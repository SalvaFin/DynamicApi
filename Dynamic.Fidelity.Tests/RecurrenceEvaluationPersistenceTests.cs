using Dynamic.Fidelity.Application.Services;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Domain.Enums;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Dynamic.Fidelity.Tests;

public sealed class RecurrenceEvaluationPersistenceTests
{
    [Fact]
    public async Task HistoricalTransactionsCountWithinBusinessAndWindowAndStayUnchanged()
    {
        await using var db = new DynamicFidelityDbContext(new DbContextOptionsBuilder<DynamicFidelityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        Guid business = Guid.NewGuid();
        Guid otherBusiness = Guid.NewGuid();
        Guid customer = Guid.NewGuid();
        DateTime now = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);
        var rule = new RecurrenceRule
        {
            Id = Guid.NewGuid(), NegocioId = business, Name = "Tres en 30 días", Active = true,
            Family = RecurrenceRuleFamily.Frequency, BenefitMode = RecurrenceBenefitMode.OncePerWindow,
            Threshold = 3, WindowDays = 30, Multiplier = 2, StartsAtUtc = now.AddYears(-1)
        };
        db.RecurrenceRules.Add(rule);
        db.PointsTransactions.AddRange(
            Earn(customer, business, now.AddDays(-30)),
            Earn(customer, business, now.AddDays(-1)),
            Earn(customer, otherBusiness, now.AddDays(-1)),
            new PointsTransaction { Id = Guid.NewGuid(), UserId = customer, NegocioId = business,
                TransactionType = PointsTransactionType.TransferIn, PointsAmount = 100, CreatedAtUtc = now.AddHours(-1) });
        await db.SaveChangesAsync();

        var service = new RecurrenceEvaluationService(db);
        var resolution = await service.ResolveAsync(business, customer, now, Guid.NewGuid(), default);
        Assert.Equal(3, resolution.VisitNumber);
        Assert.Equal(2m, resolution.Multiplier);
        var newTransaction = Earn(customer, business, now);
        newTransaction.PointsAmount = RecurrenceEvaluationService.CalculatePoints(10, 1, resolution);
        RecurrenceEvaluationService.Stamp(newTransaction, 1, 10, resolution);
        db.PointsTransactions.Add(newTransaction);
        await db.SaveChangesAsync();

        rule.Multiplier = 4;
        await db.SaveChangesAsync();
        Assert.Equal(20, newTransaction.PointsAmount);
        Assert.Equal(2m, newTransaction.BenefitMultiplierSnapshot);
        Assert.Contains("\"Multiplier\":2", newTransaction.RecurrenceSnapshotJson);
        Assert.Equal(1m, (await service.ResolveAsync(business, customer, now.AddMinutes(1),
            Guid.NewGuid(), default)).Multiplier);
    }

    private static PointsTransaction Earn(Guid userId, Guid negocioId, DateTime atUtc) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, NegocioId = negocioId,
        TransactionType = PointsTransactionType.BackofficeEarn,
        AmountEuros = 10, PointsAmount = 10, CreatedAtUtc = atUtc
    };
}
