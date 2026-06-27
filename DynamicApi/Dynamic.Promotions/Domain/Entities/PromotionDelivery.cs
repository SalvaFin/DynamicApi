using Dynamic.Promotions.Domain.Enums;
using Dynamic.Users.Domain.Enums;

namespace Dynamic.Promotions.Domain.Entities;

public class PromotionDelivery
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid RecipientId { get; set; }
    public Guid UserId { get; set; }
    public Guid UserDeviceId { get; set; }
    public PushProvider Provider { get; set; }
    public PromotionDeliveryStatus Status { get; set; } = PromotionDeliveryStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }

    public PromotionCampaign Campaign { get; set; } = null!;
    public PromotionRecipient Recipient { get; set; } = null!;
}
