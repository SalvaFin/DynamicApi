using Dynamic.Fidelity.Domain.Entities;

namespace Dynamic.Fidelity.Application.Services;

public sealed record RecurrenceVisit(Guid TransactionId, DateTime AtUtc, IReadOnlyDictionary<Guid, DateTime>? RuleActivations = null);
public sealed record RecurrenceMatch(RecurrenceRule Rule, int VisitNumber, int VisitsInWindow, DateTime ActivatedAtUtc);
public sealed record RecurrenceResolution(int VisitNumber, decimal Multiplier, IReadOnlyList<RecurrenceMatch> Applied,
    IReadOnlyList<RecurrenceMatch> Eligible);

public static class RecurrenceRuleEngine
{
    // The current, valid accrual is included exactly once. Visit ordering is transaction time then ID.
    public static RecurrenceResolution Resolve(IEnumerable<RecurrenceRule> rules, IEnumerable<RecurrenceVisit> previous,
        DateTime atUtc, Guid currentTransactionId)
    {
        List<RecurrenceVisit> visits = previous.Where(x => x.AtUtc <= atUtc && x.TransactionId != currentTransactionId)
            .OrderBy(x => x.AtUtc).ThenBy(x => x.TransactionId).ToList();
        int ordinal = visits.Count + 1;
        List<RecurrenceMatch> eligible = [];
        foreach (RecurrenceRule rule in rules.Where(x => x.Active && x.StartsAtUtc <= atUtc &&
                     (!x.EndsAtUtc.HasValue || atUtc < x.EndsAtUtc.Value)))
        {
            int windowCount = rule.WindowDays is > 0
                ? visits.Count(x => x.AtUtc >= atUtc.AddDays(-rule.WindowDays.Value)) + 1 : 0;
            DateTime? activation = rule.Family switch
            {
                RecurrenceRuleFamily.Milestone => MilestoneApplies(rule, visits, ordinal, atUtc),
                RecurrenceRuleFamily.Frequency => FrequencyApplies(rule, visits, windowCount, atUtc),
                _ => null
            };
            if (activation.HasValue && rule.MaxActivationsPerCustomer is > 0 &&
                visits.Where(x => x.RuleActivations?.ContainsKey(rule.Id) == true)
                    .Select(x => x.RuleActivations![rule.Id]).Distinct().Count() >=
                    rule.MaxActivationsPerCustomer.Value &&
                !visits.Any(x => x.RuleActivations?.TryGetValue(rule.Id, out DateTime existing) == true &&
                    existing == activation.Value))
                activation = null;
            if (activation.HasValue) eligible.Add(new(rule, ordinal, windowCount, activation.Value));
        }

        List<RecurrenceMatch> ordered = eligible.OrderByDescending(x => x.Rule.Priority)
            .ThenBy(x => x.Rule.Id).ToList();
        List<RecurrenceMatch> applied = [];
        foreach (RecurrenceMatch match in ordered)
        {
            if (applied.Count > 0 && (applied.Any(x => x.Rule.Compatibility == RecurrenceCompatibility.Exclusive) ||
                                       match.Rule.Compatibility == RecurrenceCompatibility.Exclusive)) continue;
            if (applied.Aggregate(1m, (value, x) => value * x.Rule.Multiplier) * match.Rule.Multiplier > 10m)
                continue;
            applied.Add(match);
        }
        decimal multiplier = applied.Aggregate(1m, (value, match) => value * match.Rule.Multiplier);
        return new(ordinal, multiplier, applied, ordered);
    }

    private static DateTime? MilestoneApplies(RecurrenceRule rule, List<RecurrenceVisit> visits, int ordinal, DateTime atUtc)
    {
        if (rule.Threshold < 1 || ordinal < rule.Threshold) return null;
        DateTime milestoneAt = ordinal == rule.Threshold ? atUtc : visits[rule.Threshold - 1].AtUtc;
        return rule.BenefitMode switch
        {
            RecurrenceBenefitMode.CurrentVisit when ordinal == rule.Threshold => milestoneAt,
            RecurrenceBenefitMode.NextVisits when ordinal > rule.Threshold &&
                ordinal <= rule.Threshold + (rule.BenefitVisits ?? 0) => milestoneAt,
            RecurrenceBenefitMode.Permanent => milestoneAt,
            RecurrenceBenefitMode.Duration when ordinal == rule.Threshold ||
                rule.BenefitDays is > 0 && atUtc < milestoneAt.AddDays(rule.BenefitDays.Value) => milestoneAt,
            _ => null
        };
    }

    private static DateTime? FrequencyApplies(RecurrenceRule rule, List<RecurrenceVisit> visits, int windowCount, DateTime atUtc)
    {
        if (rule.WindowDays is not > 0 || rule.Threshold < 2) return null;
        List<(RecurrenceVisit Visit, DateTime Activation)> awards = visits
            .Where(x => x.RuleActivations?.ContainsKey(rule.Id) == true)
            .Select(x => (x, x.RuleActivations![rule.Id])).ToList();
        var last = awards.LastOrDefault();
        if (rule.BenefitMode == RecurrenceBenefitMode.Duration && last.Visit is not null &&
            rule.BenefitDays is > 0 && atUtc < last.Activation.AddDays(rule.BenefitDays.Value))
            return last.Activation;
        if (windowCount < rule.Threshold) return null;
        return rule.BenefitMode switch
        {
            RecurrenceBenefitMode.OncePerWindow when last.Visit is null ||
                last.Activation < atUtc.AddDays(-rule.WindowDays.Value) => atUtc,
            RecurrenceBenefitMode.RenewOnThreshold =>
                visits.Count(x => x.AtUtc >= atUtc.AddDays(-rule.WindowDays.Value) &&
                    (last.Visit is null || x.AtUtc > last.Visit.AtUtc ||
                     x.AtUtc == last.Visit.AtUtc && x.TransactionId.CompareTo(last.Visit.TransactionId) > 0)) + 1 >= rule.Threshold
                    ? atUtc : null,
            RecurrenceBenefitMode.Duration when rule.BenefitDays is > 0 &&
                visits.Count(x => x.AtUtc >= atUtc.AddDays(-rule.WindowDays.Value) &&
                    (last.Visit is null || x.AtUtc > last.Activation)) + 1 >= rule.Threshold => atUtc,
            _ => null
        };
    }
}
