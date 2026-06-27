using System.ComponentModel.DataAnnotations;

namespace Dynamic.Promotions.Application.DTOs.Requests;

public class CreatePromotionCampaignRequest
{
    [Required]
    [MaxLength(140)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(1200)]
    public string Message { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string? ImageUrl { get; set; }

    [MaxLength(80)]
    public string? ActionLabel { get; set; }

    [MaxLength(1024)]
    public string? DeepLink { get; set; }

    [MaxLength(4000)]
    public string? Conditions { get; set; }

    public DateTime? StartsAtUtc { get; set; }

    [Required]
    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? ScheduledAtUtc { get; set; }

    [MaxLength(128)]
    public string? IdempotencyKey { get; set; }

    public PromotionAudienceFiltersRequest Filters { get; set; } = new();
}
