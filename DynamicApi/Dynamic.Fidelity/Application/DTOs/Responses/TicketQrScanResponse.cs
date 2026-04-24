namespace Dynamic.Fidelity.Application.DTOs.Responses;

public class TicketQrScanResponse
{
    public Guid QrCampaignId { get; set; }
    public Guid NegocioId { get; set; }
    public Guid UserId { get; set; }
    public bool AlreadyClaimed { get; set; }
    public string Message { get; set; } = string.Empty;
    public TicketResponse Ticket { get; set; } = new();
}
