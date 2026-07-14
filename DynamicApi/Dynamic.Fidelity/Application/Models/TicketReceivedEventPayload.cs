namespace Dynamic.Fidelity.Application.Models;

public class TicketReceivedEventPayload
{
    public Guid UserId { get; set; }
    public Guid NegocioId { get; set; }
    public Guid TicketId { get; set; }
    public Guid? ParentTicketId { get; set; }
    public Guid? SourceQrCampaignId { get; set; }
    public Guid? SourcePromotionCampaignId { get; set; }
    public Guid? SourcePromotionRecipientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
