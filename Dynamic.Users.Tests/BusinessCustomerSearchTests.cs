using Dynamic.Fidelity.Application.Services;
using Dynamic.Fidelity.Application.Contracts.Services;
using Dynamic.Fidelity.Application.Models;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Dynamic.Fidelity.Infrastructure.Repositories;
using Dynamic.Negocios.Domain.Entities;
using Dynamic.Negocios.Domain.Enums;
using Dynamic.Negocios.Infrastructure.Persistence;
using Dynamic.Negocios.Infrastructure.Repositories;
using Dynamic.Users.Application.DTOs.Requests;
using Dynamic.Users.Application.Services;
using Dynamic.Users.Application.Options;
using Dynamic.Users.Domain.Entities;
using Dynamic.Users.Domain.Enums;
using Dynamic.Users.Infrastructure.Persistence;
using Dynamic.Users.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Dynamic.Users.Tests;

public sealed class BusinessCustomerSearchTests : IDisposable
{
    private readonly DynamicUsersDbContext _users = new(new DbContextOptionsBuilder<DynamicUsersDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly DynamicNegociosDbContext _businesses = new(new DbContextOptionsBuilder<DynamicNegociosDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly DynamicFidelityDbContext _fidelity = new(new DbContextOptionsBuilder<DynamicFidelityDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly Guid _worker = Guid.NewGuid();

    private UserService CreateService() => new(_users, new UserRepository(_users),
        new UserSessionRepository(_users), new UserDeviceRepository(_users),
        new UserCodeDirectoryService(_fidelity, new UserCodeDirectoryRepository(_fidelity)),
        new NegocioUsuarioVinculacionRepository(_businesses));

    private async Task<UserAccount> SeedAsync(UserRole role, UserStatus status = UserStatus.Active,
        bool completed = true)
    {
        var business = new Negocio { Id = Guid.NewGuid(), Activo = true };
        _businesses.Negocios.Add(business);
        _businesses.NegociosUsuariosVinculaciones.Add(new NegocioUsuarioVinculacion
        {
            Id = Guid.NewGuid(), NegocioId = business.Id, UserId = _worker,
            Activa = true, PuedeGestionarPuntos = true,
            TipoVinculacion = TipoVinculacionNegocioUsuario.Trabajador
        });
        await _businesses.SaveChangesAsync();
        var user = new UserAccount
        {
            Id = Guid.NewGuid(), UserName = "customer", NormalizedUserName = "CUSTOMER",
            Email = "customer@example.test", NormalizedEmail = "CUSTOMER@EXAMPLE.TEST",
            PhoneNumber = "+34 612 345 678", NormalizedPhoneNumber = "34612345678",
            Role = role, Status = status, RegistrationCompleted = completed
        };
        _users.Users.Add(user);
        await _users.SaveChangesAsync();
        return user;
    }

    [Theory]
    [InlineData(UserRole.User, " Customer@Example.Test ")]
    [InlineData(UserRole.User, "+34 612-345-678")]
    [InlineData(UserRole.PropietarioNegocio, " Customer@Example.Test ")]
    [InlineData(UserRole.PropietarioNegocio, "+34 612-345-678")]
    [InlineData(UserRole.TrabajadorNegocio, " Customer@Example.Test ")]
    [InlineData(UserRole.TrabajadorNegocio, "+34 612-345-678")]
    public async Task StaffCanFindPersonalAccountsAndResolveThemForAccrual(UserRole role, string contact)
    {
        var customer = await SeedAsync(role);
        var service = CreateService();
        var result = await service.SearchBusinessCustomerByContactAsync(_worker, false,
            new BusinessCustomerSearchRequest { Contact = contact });
        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(customer.Id, result.Data!.UserId);
        Assert.False(string.IsNullOrWhiteSpace(result.Data.UserCode));
        var accrualTarget = await service.GetBusinessCustomerByIdAsync(_worker, false, customer.Id);
        Assert.True(accrualTarget.Succeeded, accrualTarget.ErrorMessage);
        Assert.Equal(result.Data.UserCode, accrualTarget.Data!.UserCode);
    }

    [Theory]
    [InlineData(UserRole.User, UserStatus.Disabled, true)]
    [InlineData(UserRole.TrabajadorNegocio, UserStatus.Locked, true)]
    [InlineData(UserRole.PropietarioNegocio, UserStatus.PendingActivation, false)]
    [InlineData(UserRole.User, UserStatus.Deleted, true)]
    [InlineData(UserRole.User, UserStatus.Active, false)]
    [InlineData(UserRole.Admin, UserStatus.Active, true)]
    public async Task IneligibleAccountsRemainBlocked(UserRole role, UserStatus status, bool completed)
    {
        var customer = await SeedAsync(role, status, completed);
        var service = CreateService();
        var search = await service.SearchBusinessCustomerByContactAsync(_worker, false,
            new BusinessCustomerSearchRequest { Contact = customer.Email! });
        Assert.Equal("not_found", search.ErrorCode);
        var target = await service.GetBusinessCustomerByIdAsync(_worker, false, customer.Id);
        Assert.Equal("not_found", target.ErrorCode);
    }

    [Fact]
    public async Task SearchRequiresBusinessPermissions()
    {
        var customer = await SeedAsync(UserRole.User);
        var link = Assert.Single(_businesses.NegociosUsuariosVinculaciones);
        link.PuedeGestionarPuntos = false;
        await _businesses.SaveChangesAsync();
        var result = await CreateService().SearchBusinessCustomerByContactAsync(_worker, false,
            new BusinessCustomerSearchRequest { Contact = customer.Email! });
        Assert.Equal("forbidden", result.ErrorCode);
        Assert.Empty(_fidelity.UserCodeDirectoryEntries);
    }

    public void Dispose()
    {
        _users.Dispose();
        _businesses.Dispose();
        _fidelity.Dispose();
    }

    [Fact]
    public async Task WorkerCanRegisterNewCustomerAndRepeatWithoutDuplicating()
    {
        await SeedAsync(UserRole.User);
        var rewards = new NoWelcomeRewards();
        var service = new BusinessUserProvisioningService(_users, _businesses,
            new UserRepository(_users), new UserAuthEventRepository(_users), new PasswordHasher<UserAccount>(),
            new UserCodeDirectoryService(_fidelity, new UserCodeDirectoryRepository(_fidelity)),
            new NegocioRepository(_businesses), new NegocioUsuarioVinculacionRepository(_businesses),
            new NegocioAudienciaService(_businesses, _fidelity, rewards), rewards, null!,
            Options.Create(new UserRegistrationOptions()), NullLogger<BusinessUserProvisioningService>.Instance);
        var request = new CreateBusinessCustomerUserRequest
        {
            Contact = "new@example.test", Nombre = "Cliente", Apellidos = "Prueba",
            BirthDate = new DateTime(1990, 1, 1), Gender = UserGender.Mujer,
            PostalCode = "28013", Province = SpanishProvince.Madrid,
            TermsAccepted = true, PrivacyPolicyAccepted = true, LinkToBusiness = true
        };
        var businessId = Assert.Single(_businesses.Negocios).Id;
        var result = await service.CreateCustomerByBusinessStaffAsync(businessId, _worker, false,
            request, null, null);
        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.True(result.Data!.Created);
        Assert.True(result.Data.LinkedNow);
        var customerId = result.Data.User.Id;
        Assert.True((await CreateService().SearchBusinessCustomerByContactAsync(_worker, false,
            new BusinessCustomerSearchRequest { Contact = request.Contact })).Succeeded);
        var repeat = await service.CreateCustomerByBusinessStaffAsync(businessId, _worker, false,
            request, null, null);
        Assert.True(repeat.Succeeded, repeat.ErrorMessage);
        Assert.False(repeat.Data!.Created);
        Assert.False(repeat.Data.LinkedNow);
        Assert.Equal(customerId, repeat.Data.User.Id);
        Assert.Single(_users.Users.Where(user => user.Email == request.Contact));
        Assert.Single(_businesses.NegociosAudiencias.Where(item => item.UserId == customerId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RegistrationLinksAndRequestsWelcomeRewardOnlyWhenSelected(bool linkToBusiness)
    {
        await SeedAsync(UserRole.User);
        var rewards = new NoWelcomeRewards { WelcomeAvailable = true };
        var service = new BusinessUserProvisioningService(_users, _businesses,
            new UserRepository(_users), new UserAuthEventRepository(_users), new PasswordHasher<UserAccount>(),
            new UserCodeDirectoryService(_fidelity, new UserCodeDirectoryRepository(_fidelity)),
            new NegocioRepository(_businesses), new NegocioUsuarioVinculacionRepository(_businesses),
            new NegocioAudienciaService(_businesses, _fidelity, rewards), rewards, null!,
            Options.Create(new UserRegistrationOptions()), NullLogger<BusinessUserProvisioningService>.Instance);
        var request = new CreateBusinessCustomerUserRequest
        {
            Contact = "optional@example.test", Nombre = "Cliente", Apellidos = "Prueba",
            BirthDate = new DateTime(1990, 1, 1), Gender = UserGender.Mujer,
            PostalCode = "28013", Province = SpanishProvince.Madrid,
            TermsAccepted = true, PrivacyPolicyAccepted = true, LinkToBusiness = linkToBusiness
        };
        var businessId = Assert.Single(_businesses.Negocios).Id;
        var result = await service.CreateCustomerByBusinessStaffAsync(businessId, _worker, false, request, null, null);
        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.True(result.Data!.Created);
        Assert.Equal(linkToBusiness, result.Data.LinkedNow);
        Assert.Equal(linkToBusiness, result.Data.FormaParteAudiencia);
        Assert.Equal(linkToBusiness, result.Data.ReceivedWelcomeTicket);
        Assert.Equal(linkToBusiness ? 1 : 0, rewards.WelcomeCalls);
        Assert.Equal(linkToBusiness ? 1 : 0, _businesses.NegociosAudiencias.Count());

        // A registration-only customer can be linked later, without creating another account.
        request.LinkToBusiness = true;
        var linked = await service.CreateCustomerByBusinessStaffAsync(businessId, _worker, false, request, null, null);
        Assert.True(linked.Succeeded, linked.ErrorMessage);
        Assert.False(linked.Data!.Created);
        Assert.Equal(!linkToBusiness, linked.Data.LinkedNow);
        Assert.Equal(!linkToBusiness, linked.Data.ReceivedWelcomeTicket);
        Assert.Equal(result.Data.User.Id, linked.Data.User.Id);
        Assert.Equal(1, rewards.WelcomeCalls);
        Assert.Single(_businesses.NegociosAudiencias);

        // Leaving the option off later preserves an existing membership and never grants again.
        request.LinkToBusiness = false;
        var repeat = await service.CreateCustomerByBusinessStaffAsync(businessId, _worker, false, request, null, null);
        Assert.True(repeat.Succeeded, repeat.ErrorMessage);
        Assert.False(repeat.Data!.LinkedNow);
        Assert.False(repeat.Data.ReceivedWelcomeTicket);
        Assert.True(repeat.Data.FormaParteAudiencia);
        Assert.Equal(1, rewards.WelcomeCalls);
        Assert.Single(_users.Users.Where(user => user.Email == request.Contact));
    }

    private sealed class NoWelcomeRewards : IRegistrationRewardService
    {
        public int WelcomeCalls { get; private set; }
        public bool WelcomeAvailable { get; init; }
        public Task<bool> AssignBusinessWelcomeTicketAsync(Guid negocioId, Guid userId, CancellationToken cancellationToken = default)
        {
            WelcomeCalls++;
            return Task.FromResult(WelcomeAvailable);
        }
        public Task<bool> AssignBusinessReferralTicketAsync(Guid negocioId, Guid userId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<bool> ValidateQrTokenAsync(string qrToken, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task PreparePendingAssignmentAsync(Guid userId, string qrToken, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task FinalizePendingAssignmentsAsync(Guid userId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<WelcomeTicketClaimResult?> ClaimTicketFromQrAsync(Guid userId, string qrToken, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
