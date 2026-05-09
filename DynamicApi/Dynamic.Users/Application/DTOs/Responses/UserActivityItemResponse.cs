namespace Dynamic.Users.Application.DTOs.Responses;

public class UserActivityItemResponse
{
    public string Id { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public UserActivityBusinessSummaryResponse? Negocio { get; set; }
    public Guid? TicketId { get; set; }
    public Guid? TransactionId { get; set; }
    public int? PointsAmount { get; set; }
    public int? BalanceAfter { get; set; }
    public decimal? AmountEuros { get; set; }
    public string? Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
