using Dynamic.Fidelity.Application.Services;
using Dynamic.Fidelity.Domain.Entities;
using Xunit;

namespace Dynamic.Fidelity.Tests;

public sealed class RecurrenceRuleEngineTests
{
    private static readonly DateTime Now = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);

    private static RecurrenceRule Rule(RecurrenceRuleFamily family, RecurrenceBenefitMode mode, int threshold = 3) => new()
    {
        Id = Guid.NewGuid(), Active = true, Family = family, BenefitMode = mode,
        Threshold = threshold, WindowDays = 30, BenefitDays = 7, BenefitVisits = 2,
        Multiplier = 2m, StartsAtUtc = Now.AddYears(-1)
    };

    private static RecurrenceVisit Visit(int daysAgo, IReadOnlyDictionary<Guid, DateTime>? activations = null)
        => new(Guid.NewGuid(), Now.AddDays(-daysAgo), activations);

    [Fact]
    public void FifthVisitOnlyAwardsFifth()
    {
        var rule = Rule(RecurrenceRuleFamily.Milestone, RecurrenceBenefitMode.CurrentVisit, 5);
        var four = Enumerable.Range(1, 4).Select(i => Visit(5 - i)).ToArray();
        Assert.Equal(2m, RecurrenceRuleEngine.Resolve([rule], four, Now, Guid.NewGuid()).Multiplier);
        Assert.Equal(1m, RecurrenceRuleEngine.Resolve([rule], [.. four, Visit(0)], Now.AddMinutes(1), Guid.NewGuid()).Multiplier);
    }

    [Fact]
    public void ThirdVisitAtInclusiveThirtyDayBoundaryQualifies()
    {
        var rule = Rule(RecurrenceRuleFamily.Frequency, RecurrenceBenefitMode.OncePerWindow);
        Assert.Equal(2m, RecurrenceRuleEngine.Resolve([rule], [Visit(30), Visit(1)], Now, Guid.NewGuid()).Multiplier);
        Assert.Equal(1m, RecurrenceRuleEngine.Resolve([rule], [Visit(31), Visit(1)], Now, Guid.NewGuid()).Multiplier);
    }

    [Fact]
    public void RetriedTransactionIdIsNotAnotherVisit()
    {
        var rule = Rule(RecurrenceRuleFamily.Milestone, RecurrenceBenefitMode.CurrentVisit, 2);
        var previous = Visit(1);
        Assert.Equal(1m, RecurrenceRuleEngine.Resolve([rule], [previous], Now, previous.TransactionId).Multiplier);
    }

    [Fact]
    public void HighestPriorityExclusiveWins()
    {
        var low = Rule(RecurrenceRuleFamily.Milestone, RecurrenceBenefitMode.Permanent, 1);
        var high = Rule(RecurrenceRuleFamily.Milestone, RecurrenceBenefitMode.Permanent, 1);
        high.Priority = 10;
        high.Multiplier = 3m;
        Assert.Equal(3m, RecurrenceRuleEngine.Resolve([low, high], [], Now, Guid.NewGuid()).Multiplier);
        high.Compatibility = low.Compatibility = RecurrenceCompatibility.Stack;
        Assert.Equal(6m, RecurrenceRuleEngine.Resolve([low, high], [], Now, Guid.NewGuid()).Multiplier);
    }

    [Fact]
    public void DurationExpiresAtExactInstant()
    {
        var rule = Rule(RecurrenceRuleFamily.Milestone, RecurrenceBenefitMode.Duration, 1);
        Assert.Equal(2m, RecurrenceRuleEngine.Resolve([rule], [Visit(6)], Now, Guid.NewGuid()).Multiplier);
        Assert.Equal(1m, RecurrenceRuleEngine.Resolve([rule], [Visit(7)], Now, Guid.NewGuid()).Multiplier);
    }

    [Fact]
    public void NextVisitsExcludesMilestoneAndStopsAfterLimit()
    {
        var rule = Rule(RecurrenceRuleFamily.Milestone, RecurrenceBenefitMode.NextVisits, 2);
        Assert.Empty(RecurrenceRuleEngine.Resolve([rule], [Visit(3)], Now.AddDays(-2), Guid.NewGuid()).Applied);
        Assert.Single(RecurrenceRuleEngine.Resolve([rule], [Visit(3), Visit(2)], Now.AddDays(-1), Guid.NewGuid()).Applied);
        Assert.Single(RecurrenceRuleEngine.Resolve([rule], [Visit(3), Visit(2), Visit(1)], Now, Guid.NewGuid()).Applied);
        Assert.Empty(RecurrenceRuleEngine.Resolve([rule], [Visit(4), Visit(3), Visit(2), Visit(1)], Now, Guid.NewGuid()).Applied);
    }

    [Fact]
    public void RenewRequiresThreeNewVisitsSinceLastAward()
    {
        var rule = Rule(RecurrenceRuleFamily.Frequency, RecurrenceBenefitMode.RenewOnThreshold);
        var awarded = Visit(3, new Dictionary<Guid, DateTime> { [rule.Id] = Now.AddDays(-3) });
        Assert.Empty(RecurrenceRuleEngine.Resolve([rule], [Visit(5), Visit(4), awarded, Visit(2)], Now, Guid.NewGuid()).Applied);
        Assert.Single(RecurrenceRuleEngine.Resolve([rule], [Visit(5), Visit(4), awarded, Visit(2), Visit(1)], Now, Guid.NewGuid()).Applied);
    }

    [Fact]
    public void SnapshotKeepsOriginalRuleAfterEdit()
    {
        var rule = Rule(RecurrenceRuleFamily.Milestone, RecurrenceBenefitMode.CurrentVisit, 1);
        var resolution = RecurrenceRuleEngine.Resolve([rule], [], Now, Guid.NewGuid());
        var transaction = new Dynamic.Fidelity.Domain.Entities.PointsTransaction();
        RecurrenceEvaluationService.Stamp(transaction, 3m, 30, resolution);
        string original = transaction.RecurrenceSnapshotJson!;
        rule.Multiplier = 4m;
        Assert.Equal(2m, transaction.BenefitMultiplierSnapshot);
        Assert.Contains("\"Multiplier\":2", original);
        Assert.Equal(1, transaction.VisitOrdinalSnapshot);
    }

    [Fact]
    public void OncePerWindowDoesNotAwardTwice()
    {
        var rule = Rule(RecurrenceRuleFamily.Frequency, RecurrenceBenefitMode.OncePerWindow);
        var awarded = Visit(1, new Dictionary<Guid, DateTime> { [rule.Id] = Now.AddDays(-1) });
        Assert.Equal(1m, RecurrenceRuleEngine.Resolve([rule], [Visit(4), Visit(2), awarded], Now, Guid.NewGuid()).Multiplier);
    }

    [Fact]
    public void FrequencyDurationKeepsOriginalActivation()
    {
        var rule = Rule(RecurrenceRuleFamily.Frequency, RecurrenceBenefitMode.Duration);
        var activation = Now.AddDays(-6);
        var awarded = new RecurrenceVisit(Guid.NewGuid(), activation, new Dictionary<Guid, DateTime> { [rule.Id] = activation });
        var result = RecurrenceRuleEngine.Resolve([rule], [awarded], Now, Guid.NewGuid());
        Assert.Equal(activation, Assert.Single(result.Applied).ActivatedAtUtc);
        Assert.Empty(RecurrenceRuleEngine.Resolve([rule], [awarded], Now.AddDays(1), Guid.NewGuid()).Applied);
    }

    [Fact]
    public void ActivationLimitStopsRenewalButKeepsExistingDuration()
    {
        var rule = Rule(RecurrenceRuleFamily.Frequency, RecurrenceBenefitMode.Duration);
        rule.MaxActivationsPerCustomer = 1;
        DateTime activated = Now.AddDays(-6);
        var awarded = new RecurrenceVisit(Guid.NewGuid(), activated,
            new Dictionary<Guid, DateTime> { [rule.Id] = activated });
        Assert.Single(RecurrenceRuleEngine.Resolve([rule], [awarded], Now, Guid.NewGuid()).Applied);
        Assert.Empty(RecurrenceRuleEngine.Resolve([rule],
            [awarded, Visit(2), Visit(1)], Now.AddDays(2), Guid.NewGuid()).Applied);
    }
}
