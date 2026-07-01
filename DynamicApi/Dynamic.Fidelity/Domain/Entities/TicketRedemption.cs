namespace Dynamic.Fidelity.Domain.Entities;

public class TicketRedemption
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid NegocioId { get; set; }
    public Guid UserId { get; set; }
    public Guid ValidatedByUserId { get; set; }
    public Guid? ParentTicketId { get; set; }
    public Guid? SourceQrCampaignId { get; set; }
    public Guid? SourcePromotionCampaignId { get; set; }
    public Guid? SourcePromotionRecipientId { get; set; }
    public string TicketNombreSnapshot { get; set; } = string.Empty;
    public string TicketTipoSnapshot { get; set; } = string.Empty;
    public string TicketCategoriaSnapshot { get; set; } = string.Empty;
    public string? TicketCodeSnapshot { get; set; }
    public int UsageNumber { get; set; }
    public decimal? PurchaseAmount { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? FinalAmount { get; set; }
    public bool? MinimumSpendSatisfied { get; set; }
    public string? StoreReference { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
