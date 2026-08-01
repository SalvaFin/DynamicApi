using Dynamic.Promotions.Domain.Enums;

namespace Dynamic.Promotions.Domain.Entities;

public class PromotionRecipient
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid UserId { get; set; }
    public PromotionRecipientStatus Status { get; set; } = PromotionRecipientStatus.Received;
    public DateTime ReceivedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
    public DateTime? PresentedAtUtc { get; set; }
    public DateTime? DismissedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public PromotionCampaign Campaign { get; set; } = null!;
    public ICollection<PromotionDelivery> Deliveries { get; set; } = [];
    public ICollection<PromotionEmailDelivery> EmailDeliveries { get; set; } = [];
}
