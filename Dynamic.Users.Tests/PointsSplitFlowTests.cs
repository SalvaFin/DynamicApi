using System.Security.Claims;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Domain.Enums;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Dynamic.Negocios.Domain.Entities;
using Dynamic.Negocios.Infrastructure.Persistence;
using Dynamic.Notify.Application.Contracts;
using Dynamic.Notify.Application.Models;
using Dynamic.Users.Application.DTOs.Requests;
using Dynamic.Users.Application.DTOs.Responses;
using Dynamic.Users.Application.Services;
using Dynamic.Users.Controllers;
using Dynamic.Users.Domain.Entities;
using Dynamic.Users.Domain.Enums;
using Dynamic.Users.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Dynamic.Users.Tests;

public sealed class PointsSplitFlowTests : IDisposable
{
    private readonly DynamicUsersDbContext _users = new(new DbContextOptionsBuilder<DynamicUsersDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly DynamicNegociosDbContext _businesses = new(new DbContextOptionsBuilder<DynamicNegociosDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly DynamicFidelityDbContext _fidelity = new(new DbContextOptionsBuilder<DynamicFidelityDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly Guid _owner = Guid.NewGuid();
    private readonly Guid _business = Guid.NewGuid();
    private PointsSplitService Service => new(_fidelity, _users, _businesses, new NoEvents());

    private PointsSplitController Controller => new(Service)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, _owner.ToString())], "test"))
            }
        }
    };

    private PointsTransaction Earning(int minutes, PointsTransactionType type = PointsTransactionType.Earn,
        int points = 200, Guid? userId = null) => new()
    {
        Id = Guid.NewGuid(), UserId = userId ?? _owner, NegocioId = _business,
        PointsAmount = points, TransactionType = type, OperationId = Guid.NewGuid(),
        CreatedAtUtc = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc).AddMinutes(minutes)
    };

    [Fact]
    public async Task LatestTwentyExcludeOtherUsersDebitsAndZeroPoints()
    {
        var own = Enumerable.Range(1, 25).Select(index => Earning(index)).ToArray();
        _fidelity.PointsTransactions.AddRange(own);
        _fidelity.PointsTransactions.AddRange(Earning(100, userId: Guid.NewGuid()),
            Earning(100, PointsTransactionType.Spend), Earning(100, points: 0),
            Earning(100, points: -20), Earning(100, PointsTransactionType.SplitOut));
        _businesses.Negocios.Add(new Negocio { Id = _business, NombreComercial = "Restaurante" });
        await _fidelity.SaveChangesAsync();
        await _businesses.SaveChangesAsync();

        var result = await Service.GetRecentEarningsAsync(_owner, default);
        Assert.Equal(own.Reverse().Take(20).Select(item => item.Id), result.Select(item => item.TransactionId));
        Assert.All(result, item => { Assert.Equal("Restaurante", item.BusinessName); Assert.True(item.CanSplit); });
    }

    [Theory]
    [InlineData(PointsTransactionType.Earn, true)]
    [InlineData(PointsTransactionType.BackofficeEarn, false)]
    [InlineData(PointsTransactionType.TransferIn, false)]
    [InlineData(PointsTransactionType.SplitIn, false)]
    public async Task IncomingPointsAreListedButOnlyValidatedPurchasesCanBeSplit(PointsTransactionType type, bool canSplit)
    {
        _fidelity.PointsTransactions.Add(Earning(0, type));
        await _fidelity.SaveChangesAsync();
        var item = Assert.Single(await Service.GetRecentEarningsAsync(_owner, default));
        Assert.Equal(canSplit, item.CanSplit);
    }

    [Fact]
    public async Task AlreadySplitPurchaseOffersExistingSplitAndCannotBeSplitAgain()
    {
        var earning = Earning(0);
        var split = new PointsSplit { Id = Guid.NewGuid(), SourceTransactionId = earning.Id,
            OwnerUserId = _owner, NegocioId = _business };
        _fidelity.PointsTransactions.Add(earning);
        _fidelity.PointsSplits.Add(split);
        await _fidelity.SaveChangesAsync();
        var item = Assert.Single(await Service.GetRecentEarningsAsync(_owner, default));
        Assert.False(item.CanSplit);
        Assert.Equal(split.Id, item.SplitId);
        Assert.NotNull(await Service.GetAsync(_owner, earning.Id, default));
    }

    [Fact]
    public async Task UnvalidatedPurchaseAndOnePointCannotBeSplit()
    {
        var unvalidated = Earning(0);
        unvalidated.OperationId = null;
        _fidelity.PointsTransactions.AddRange(unvalidated, Earning(1, points: 1));
        await _fidelity.SaveChangesAsync();
        Assert.All(await Service.GetRecentEarningsAsync(_owner, default), item => Assert.False(item.CanSplit));
    }

    [Fact]
    public async Task EmptyAccountReturnsEmptyList()
        => Assert.Empty(await Service.GetRecentEarningsAsync(_owner, default));

    private async Task<UserAccount> Customer(bool confirmed = true, bool active = true, bool completed = true)
    {
        var user = new UserAccount { Id = Guid.NewGuid(), Email = "friend@example.test",
            NormalizedEmail = "FRIEND@EXAMPLE.TEST", EmailConfirmed = confirmed,
            Status = active ? UserStatus.Active : UserStatus.Disabled, RegistrationCompleted = completed,
            Role = UserRole.User, DisplayName = "Comensal" };
        _users.Users.Add(user);
        await _users.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task NormalCustomerQrAndNormalizedEmailResolveSameCustomer()
    {
        var user = await Customer();
        var qr = Assert.IsType<OkObjectResult>(await Controller.ResolveRecipient(
            new ResolveSplitRecipientRequest { CustomerQrPayload = " " + user.Id.ToString("D") + " " }, default));
        var email = Assert.IsType<OkObjectResult>(await Controller.ResolveRecipient(
            new ResolveSplitRecipientRequest { Email = " Friend@Example.Test " }, default));
        Assert.Equal(user.Id, Assert.IsType<SplitRecipientResponse>(qr.Value).UserId);
        Assert.Equal(user.Id, Assert.IsType<SplitRecipientResponse>(email.Value).UserId);
    }

    [Theory]
    [InlineData("dynamic-points-share:old-token")]
    [InlineData("https://appdynamic.es/qr-claim?qr=campaign")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task RejectsSpecialShareQrCampaignQrAndEmptyId(string payload)
        => Assert.IsType<BadRequestObjectResult>(await Controller.ResolveRecipient(
            new ResolveSplitRecipientRequest { CustomerQrPayload = payload }, default));

    [Fact]
    public async Task RequiresExactlyOneLookupMethod()
    {
        Assert.IsType<BadRequestObjectResult>(await Controller.ResolveRecipient(new(), default));
        Assert.IsType<BadRequestObjectResult>(await Controller.ResolveRecipient(new()
            { Email = "friend@example.test", CustomerQrPayload = Guid.NewGuid().ToString() }, default));
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task InvalidCustomerCannotBeAddedThroughEmailOrQr(bool verified, bool active, bool completed)
    {
        var user = await Customer(verified, active, completed);
        Assert.IsType<NotFoundObjectResult>(await Controller.ResolveRecipient(
            new() { CustomerQrPayload = user.Id.ToString() }, default));
        Assert.IsType<NotFoundObjectResult>(await Controller.ResolveRecipient(
            new() { Email = user.Email }, default));
    }

    [Fact]
    public async Task OwnQrCannotAddOwnerTwice()
    {
        var user = await Customer();
        _users.Users.Remove(user);
        await _users.SaveChangesAsync();
        user.Id = _owner;
        _users.Users.Add(user);
        await _users.SaveChangesAsync();
        Assert.IsType<BadRequestObjectResult>(await Controller.ResolveRecipient(
            new() { CustomerQrPayload = _owner.ToString() }, default));
    }

    [Fact]
    public async Task MissingIdentityCannotReadEarnings()
    {
        var controller = new PointsSplitController(Service) { ControllerContext = new ControllerContext
            { HttpContext = new DefaultHttpContext() } };
        Assert.IsType<UnauthorizedResult>(await controller.GetRecentEarnings(default));
    }

    public void Dispose() { _users.Dispose(); _businesses.Dispose(); _fidelity.Dispose(); }
    private sealed class NoEvents : IUserEventPublisher
    {
        public Task PublishAsync(Guid userId, UserAppEvent appEvent, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Reading earnings or resolving users must not publish events.");
    }
}
