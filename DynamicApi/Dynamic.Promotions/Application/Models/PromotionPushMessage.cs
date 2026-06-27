namespace Dynamic.Promotions.Application.Models;

public class PromotionPushMessage
{
    public string Token { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public Guid PromotionRecipientId { get; set; }
    public Guid CampaignId { get; set; }
    public Guid NegocioId { get; set; }
    public string? DeepLink { get; set; }
}

public class PromotionPushResult
{
    public bool Succeeded { get; init; }
    public bool InvalidToken { get; init; }
    public bool Retryable { get; init; }
    public string? ProviderMessageId { get; init; }
    public string? Error { get; init; }

    public static PromotionPushResult Success(string? providerMessageId)
        => new() { Succeeded = true, ProviderMessageId = providerMessageId };

    public static PromotionPushResult Failure(string error, bool retryable, bool invalidToken = false)
        => new() { Error = error, Retryable = retryable, InvalidToken = invalidToken };
}
