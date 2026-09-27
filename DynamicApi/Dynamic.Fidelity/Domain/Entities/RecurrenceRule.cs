namespace Dynamic.Fidelity.Domain.Entities;

public enum RecurrenceRuleFamily { Milestone, Frequency }
public enum RecurrenceBenefitMode { CurrentVisit, NextVisits, Duration, Permanent, OncePerWindow, RenewOnThreshold }
public enum RecurrenceCompatibility { Exclusive, Stack }

public sealed class RecurrenceRule
{
    public Guid Id { get; set; }
    public Guid NegocioId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Active { get; set; }
    public RecurrenceRuleFamily Family { get; set; }
    public RecurrenceBenefitMode BenefitMode { get; set; }
    public RecurrenceCompatibility Compatibility { get; set; } = RecurrenceCompatibility.Exclusive;
    public int Threshold { get; set; }
    public int? WindowDays { get; set; }
    public int? BenefitVisits { get; set; }
    public int? BenefitDays { get; set; }
    public int? MaxActivationsPerCustomer { get; set; }
    public decimal Multiplier { get; set; } = 1m;
    public int Priority { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime? EndsAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
