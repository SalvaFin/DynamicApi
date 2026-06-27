namespace Dynamic.Promotions.Application.DTOs.Responses;

public class ReceivedPromotionResponse
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public PromotionBusinessSummaryResponse Negocio { get; set; } = new();
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? ActionLabel { get; set; }
    public string? DeepLink { get; set; }
    public string? Conditions { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAtUtc { get; set; }
}

public class PromotionBusinessSummaryResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
}
