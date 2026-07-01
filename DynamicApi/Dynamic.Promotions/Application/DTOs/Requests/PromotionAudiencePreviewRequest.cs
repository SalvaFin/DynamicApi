namespace Dynamic.Promotions.Application.DTOs.Requests;

public class PromotionAudiencePreviewRequest
{
    public PromotionAudienceFiltersRequest Filters { get; set; } = new();
}
