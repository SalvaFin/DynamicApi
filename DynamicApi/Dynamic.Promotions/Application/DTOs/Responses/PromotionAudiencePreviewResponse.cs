using Dynamic.Promotions.Application.DTOs.Requests;

namespace Dynamic.Promotions.Application.DTOs.Responses;

public class PromotionAudiencePreviewResponse
{
    public Guid NegocioId { get; set; }
    public int AudienceCount { get; set; }
    public int PushEligibleCount { get; set; }
    public bool BusinessPushEnabled { get; set; }
    public bool FirebasePushEnabled { get; set; }
    public bool PushAvailable { get; set; }
    public DateTime CalculatedAtUtc { get; set; }
    public PromotionAudienceFiltersRequest Filters { get; set; } = new();
}
