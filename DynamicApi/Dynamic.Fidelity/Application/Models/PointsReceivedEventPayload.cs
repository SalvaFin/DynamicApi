namespace Dynamic.Fidelity.Application.Models;

public class PointsReceivedEventPayload
{
    public Guid UserId { get; set; }
    public Guid NegocioId { get; set; }
    public Guid? TransactionId { get; set; }
    public Guid? OperationId { get; set; }
    public Guid? ValidatorUserId { get; set; }
    public Guid? CounterpartyUserId { get; set; }
    public int PointsAmount { get; set; }
    public int BalanceBefore { get; set; }
    public int BalanceAfter { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? Reference { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
