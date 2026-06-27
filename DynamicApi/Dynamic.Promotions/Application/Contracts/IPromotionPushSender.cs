using Dynamic.Promotions.Application.Models;

namespace Dynamic.Promotions.Application.Contracts;

public interface IPromotionPushSender
{
    Task<PromotionPushResult> SendAsync(PromotionPushMessage message, CancellationToken cancellationToken = default);
}
