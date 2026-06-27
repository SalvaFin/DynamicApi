using Dynamic.Promotions.Domain.Enums;

namespace Dynamic.Promotions.Domain.Entities;

public class PromotionOutboxMessage
{
    public Guid Id { get; set; }
    public Guid AggregateId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public PromotionOutboxStatus Status { get; set; } = PromotionOutboxStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTime AvailableAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? ProcessingStartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? LastError { get; set; }
}
