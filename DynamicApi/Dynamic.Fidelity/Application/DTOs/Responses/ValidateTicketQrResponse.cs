namespace Dynamic.Fidelity.Application.DTOs.Responses;

public class ValidateTicketQrResponse
{
    public Guid NegocioId { get; set; }
    public Guid TicketId { get; set; }
    public Guid UserId { get; set; }
    public Guid ValidatedByUserId { get; set; }
    public bool Used { get; set; }
    public int UsosConsumidos { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public decimal? PurchaseAmount { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? FinalAmount { get; set; }
    public bool? MinimumSpendSatisfied { get; set; }
    public string Message { get; set; } = string.Empty;
    public ValidatedTicketResponse Ticket { get; set; } = new();
}
