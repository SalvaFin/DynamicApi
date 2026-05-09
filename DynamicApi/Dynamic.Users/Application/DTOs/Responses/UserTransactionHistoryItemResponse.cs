using Dynamic.Fidelity.Domain.Enums;

namespace Dynamic.Users.Application.DTOs.Responses;

public class UserTransactionHistoryItemResponse
{
    public Guid TransactionId { get; set; }
    public Guid NegocioId { get; set; }
    public UserActivityBusinessSummaryResponse? Negocio { get; set; }
    public PointsTransactionType TransactionType { get; set; }
    public string Direction { get; set; } = string.Empty;
    public decimal? AmountEuros { get; set; }
    public int PointsAmount { get; set; }
    public int BalanceBefore { get; set; }
    public int BalanceAfter { get; set; }
    public string? Reason { get; set; }
    public string? Reference { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
