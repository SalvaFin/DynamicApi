namespace Dynamic.Fidelity.Application.DTOs.Responses;

public class TicketQrLookupResponse
{
    public Guid QrCampaignId { get; set; }
    public Guid NegocioId { get; set; }
    public string SlugPortal { get; set; } = string.Empty;
    public Guid TicketId { get; set; }
    public string QrToken { get; set; } = string.Empty;
    public string PublicUrl { get; set; } = string.Empty;
    public string LandingPath { get; set; } = string.Empty;
    public TicketResponse Ticket { get; set; } = new();
}
