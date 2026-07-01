using Dynamic.Promotions.Application.Common;
using Dynamic.Promotions.Application.DTOs.Requests;
using Dynamic.Promotions.Application.DTOs.Responses;

namespace Dynamic.Promotions.Application.Contracts;

public interface IPromotionService
{
    Task<PromotionServiceResult<PromotionCampaignResponse>> CreateCampaignAsync(
        Guid negocioId,
        Guid requesterUserId,
        bool requesterIsAdmin,
        CreatePromotionCampaignRequest request,
        CancellationToken cancellationToken = default);

    Task<PromotionServiceResult<PromotionCampaignResponse>> GetCampaignAsync(
        Guid negocioId,
        Guid campaignId,
        Guid requesterUserId,
        bool requesterIsAdmin,
        CancellationToken cancellationToken = default);

    Task<PromotionServiceResult<PromotionAudiencePreviewResponse>> PreviewAudienceAsync(
        Guid negocioId,
        Guid requesterUserId,
        bool requesterIsAdmin,
        PromotionAudiencePreviewRequest request,
        CancellationToken cancellationToken = default);

    Task<ReceivedPromotionsPageResponse> GetReceivedPromotionsAsync(
        Guid userId,
        int page,
        int pageSize,
        bool includeRead,
        CancellationToken cancellationToken = default);

    Task<bool> MarkAsReadAsync(Guid userId, Guid recipientId, CancellationToken cancellationToken = default);
}
