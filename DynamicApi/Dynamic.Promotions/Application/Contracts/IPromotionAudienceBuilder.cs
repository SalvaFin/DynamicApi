using Dynamic.Promotions.Application.DTOs.Requests;
using Dynamic.Promotions.Application.DTOs.Responses;

namespace Dynamic.Promotions.Application.Contracts;

public interface IPromotionAudienceBuilder
{
    Task BuildAsync(Guid campaignId, CancellationToken cancellationToken = default);

    Task<PromotionAudiencePreviewResponse> PreviewAsync(
        Guid negocioId,
        PromotionAudienceFiltersRequest filters,
        bool businessPushEnabled,
        CancellationToken cancellationToken = default);
}
