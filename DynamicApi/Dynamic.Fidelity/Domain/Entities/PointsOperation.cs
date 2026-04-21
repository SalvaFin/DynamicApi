using Dynamic.Fidelity.Domain.Enums;

namespace Dynamic.Fidelity.Domain.Entities;

public class PointsOperation
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid NegocioId { get; set; }
    public decimal AmountEuros { get; set; }
    public decimal RatioSnapshot { get; set; }
    public int ExpectedPoints { get; set; }
    public int ValidationAttempts { get; set; }
    public int MaxValidationAttempts { get; set; } = 5;
    public PointsOperationStatus Status { get; set; } = PointsOperationStatus.Pending;
    public string? CancelReason { get; set; }
    public Guid? CompletedTransactionId { get; set; }
    public Guid? ValidatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? ValidatedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
}
