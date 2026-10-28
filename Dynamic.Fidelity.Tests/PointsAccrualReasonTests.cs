using Dynamic.Fidelity.Application.Services;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Domain.Enums;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Dynamic.Fidelity.Tests;

public sealed class PointsAccrualReasonTests
{
    private static DynamicFidelityDbContext Database() => new(new DbContextOptionsBuilder<DynamicFidelityDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task NewBusinessesAlwaysHaveOneUnlimitedCheckoutReason()
    {
        await using var db = Database();
        var service = new PointsAccrualReasonService(db);
        Guid business = Guid.NewGuid();
        var reason = Assert.Single(await service.ListAsync(business, false, default));
        Assert.Equal("Paso por caja", reason.Name);
        Assert.True(reason.IsDefault && reason.Active);
        Assert.False(reason.UniquePerCustomer);
        Assert.Equal(business, reason.NegocioId);
        Assert.Equal(reason.Id, (await service.ResolveAsync(business, null, default)).Data!.Id);
        Assert.NotEqual(reason.Id, Assert.Single(await service.ListAsync(Guid.NewGuid(), false, default)).Id);
    }

    [Fact]
    public async Task UniqueReasonIsScopedToBusinessCustomerAndStableReasonId()
    {
        await using var db = Database();
        Guid business = Guid.NewGuid(), otherBusiness = Guid.NewGuid(), customer = Guid.NewGuid();
        var reason = Reason(business, true);
        var other = Reason(otherBusiness, true);
        var different = Reason(business, true);
        db.PointsAccrualReasons.AddRange(reason, other, different);
        var transaction = Credit(business, customer, reason);
        db.PointsTransactions.Add(transaction);
        await db.SaveChangesAsync();
        var service = new PointsAccrualReasonService(db);

        Assert.Equal("conflict", (await service.ValidateCustomersAsync(reason, [customer], default)).ErrorCode);
        Assert.True((await service.ValidateCustomersAsync(other, [customer], default)).Succeeded);
        Assert.True((await service.ValidateCustomersAsync(different, [customer], default)).Succeeded);
        Assert.True((await service.ValidateCustomersAsync(reason, [Guid.NewGuid()], default)).Succeeded);
        Assert.False((await service.ResolveAsync(otherBusiness, reason.Id, default)).Succeeded);
    }

    [Fact]
    public async Task UnlimitedReasonAllowsReuseAndEnablingUniqueIncludesEarlierUses()
    {
        await using var db = Database();
        Guid business = Guid.NewGuid(), customer = Guid.NewGuid();
        var reason = Reason(business, false);
        db.PointsAccrualReasons.Add(reason);
        db.PointsTransactions.Add(Credit(business, customer, reason));
        await db.SaveChangesAsync();
        var service = new PointsAccrualReasonService(db);
        Assert.True((await service.ValidateCustomersAsync(reason, [customer], default)).Succeeded);
        Assert.Null(Assert.Single(db.PointsTransactions).UniqueAccrualReasonId);

        reason.UniquePerCustomer = true;
        reason.Name = "Reseña con otro nombre";
        await db.SaveChangesAsync();
        var failure = await service.ValidateCustomersAsync(reason, [customer], default);
        Assert.Equal("conflict", failure.ErrorCode);
        Assert.Contains(reason.Name, failure.ErrorMessage);
        Assert.Equal("Reseña en Google", Assert.Single(db.PointsTransactions).Reason);

        reason.Active = false;
        await db.SaveChangesAsync();
        Assert.False((await service.ResolveAsync(business, reason.Id, default)).Succeeded);
        reason.Active = true;
        await db.SaveChangesAsync();
        Assert.False((await service.ValidateCustomersAsync(reason, [customer], default)).Succeeded);
    }

    [Fact]
    public async Task GroupEligibilityRejectsIfAnyCustomerHasAlreadyUsedTheReason()
    {
        await using var db = Database();
        Guid business = Guid.NewGuid(), used = Guid.NewGuid(), eligible = Guid.NewGuid();
        var reason = Reason(business, true);
        db.PointsTransactions.Add(Credit(business, used, reason));
        await db.SaveChangesAsync();
        var service = new PointsAccrualReasonService(db);
        Assert.False((await service.ValidateCustomersAsync(reason, [eligible, used], default)).Succeeded);
        Assert.True((await service.ValidateCustomersAsync(reason, [eligible], default)).Succeeded);
    }

    [Fact]
    public async Task InactiveAndForeignReasonsCannotBeSelectedAndOnlyActiveReasonsAreListed()
    {
        await using var db = Database();
        Guid business = Guid.NewGuid();
        var inactive = Reason(business, true); inactive.Active = false;
        var foreign = Reason(Guid.NewGuid(), true);
        db.PointsAccrualReasons.AddRange(inactive, foreign);
        await db.SaveChangesAsync();
        var service = new PointsAccrualReasonService(db);
        Assert.Single(await service.ListAsync(business, false, default));
        Assert.Equal(2, (await service.ListAsync(business, true, default)).Count);
        foreach (Guid id in new[] { inactive.Id, foreign.Id, Guid.NewGuid(), Guid.Empty })
            Assert.Equal("validation_error", (await service.ResolveAsync(business, id, default)).ErrorCode);
    }

    [Fact]
    public void DatabaseGuardIncludesBusinessCustomerAndReasonAndUnlimitedUsesHaveNullMarker()
    {
        using var db = Database();
        var index = Assert.Single(db.Model.FindEntityType(typeof(PointsTransaction))!.GetIndexes(),
            x => x.GetDatabaseName() == "UX_points_unique_accrual_reason");
        Assert.True(index.IsUnique);
        Assert.Equal(new[] { "NegocioId", "UserId", "UniqueAccrualReasonId" }, index.Properties.Select(x => x.Name));
        var transaction = Credit(Guid.NewGuid(), Guid.NewGuid(), Reason(Guid.NewGuid(), false));
        Assert.Null(transaction.UniqueAccrualReasonId);
    }

    private static PointsAccrualReason Reason(Guid business, bool unique) => new()
    {
        Id = Guid.NewGuid(), NegocioId = business, Name = "Reseña en Google", UniquePerCustomer = unique
    };

    private static PointsTransaction Credit(Guid business, Guid user, PointsAccrualReason reason)
    {
        var transaction = new PointsTransaction { Id = Guid.NewGuid(), NegocioId = business, UserId = user,
            PointsAmount = 10, TransactionType = PointsTransactionType.BackofficeEarn, CreatedAtUtc = DateTime.UtcNow };
        PointsAccrualReasonService.Stamp(transaction, reason);
        return transaction;
    }
}
