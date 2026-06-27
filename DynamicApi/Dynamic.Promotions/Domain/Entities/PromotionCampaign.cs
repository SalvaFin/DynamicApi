using Dynamic.Promotions.Domain.Enums;

namespace Dynamic.Promotions.Domain.Entities;

public class PromotionCampaign
{
    public Guid Id { get; set; }
    public Guid NegocioId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string NegocioNombreSnapshot { get; set; } = string.Empty;
    public string NegocioSlugSnapshot { get; set; } = string.Empty;
    public string? NegocioLogoUrlSnapshot { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? ActionLabel { get; set; }
    public string? DeepLink { get; set; }
    public string? Conditions { get; set; }
    public string FiltersJson { get; set; } = "{}";
    public PromotionCampaignStatus Status { get; set; } = PromotionCampaignStatus.Queued;
    public int AudienceCount { get; set; }
    public int PushEligibleCount { get; set; }
    public int PushDeliveredCount { get; set; }
    public int PushFailedCount { get; set; }
    public bool PushEnabled { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? LastError { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime ScheduledAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? AudienceProcessedAtUtc { get; set; }

    public ICollection<PromotionRecipient> Recipients { get; set; } = [];
    public ICollection<PromotionDelivery> Deliveries { get; set; } = [];
}
