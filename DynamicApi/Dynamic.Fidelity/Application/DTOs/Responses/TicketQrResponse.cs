namespace Dynamic.Fidelity.Application.DTOs.Responses;

public class TicketQrResponse
{
    public Guid QrCampaignId { get; set; }
    public Guid NegocioId { get; set; }
    public Guid TicketId { get; set; }
    public string QrToken { get; set; } = string.Empty;
    public string PublicUrl { get; set; } = string.Empty;
    public string QrSvg { get; set; } = string.Empty;
    public string ImageFormat { get; set; } = "svg";
    public DateTime CreatedAtUtc { get; set; }
}
