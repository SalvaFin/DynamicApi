namespace Dynamic.Promotions.Application.Contracts;

public interface IPromotionAudienceBuilder
{
    Task BuildAsync(Guid campaignId, CancellationToken cancellationToken = default);
}
