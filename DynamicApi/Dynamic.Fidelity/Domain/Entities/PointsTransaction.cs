using Dynamic.Fidelity.Domain.Enums;

namespace Dynamic.Fidelity.Domain.Entities;

public class PointsTransaction
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid NegocioId { get; set; }
    public Guid? PointsId { get; set; }
    public Guid? OperationId { get; set; }
    public Guid? ValidatorUserId { get; set; }
    public Guid? CounterpartyUserId { get; set; }
    public PointsTransactionType TransactionType { get; set; } = PointsTransactionType.Earn;
    public decimal? AmountEuros { get; set; }
    public int PointsAmount { get; set; }
    public int BalanceBefore { get; set; }
    public int BalanceAfter { get; set; }
    public string? UserCodeSnapshot { get; set; }
    public string? CounterpartyUserCodeSnapshot { get; set; }
    public string? Reason { get; set; }
    public string? Reference { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
