namespace Dynamic.Fidelity.Domain.Entities;

public class PendingTicketAssignment
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid NegocioId { get; set; }
    public Guid QrCampaignId { get; set; }
    public Guid TicketTemplateId { get; set; }
    public Guid? AssignedTicketId { get; set; }
    public string QrToken { get; set; } = string.Empty;
    public bool Activated { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ActivatedAtUtc { get; set; }
}
