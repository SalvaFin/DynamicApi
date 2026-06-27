using System.ComponentModel.DataAnnotations;

namespace Dynamic.Promotions.Application.Options;

public class PromotionDispatchOptions
{
    public const string SectionName = "Promotions:Dispatch";

    [Range(1, 300)]
    public int PollingIntervalSeconds { get; set; } = 3;

    [Range(1, 500)]
    public int PushBatchSize { get; set; } = 100;

    [Range(1, 20)]
    public int MaxPushAttempts { get; set; } = 5;

    [Range(0, 365)]
    public int MinimumDaysBetweenBusinessPromotions { get; set; } = 7;

    [Range(1, 100)]
    public int GlobalPromotionLimitPerWindow { get; set; } = 3;

    [Range(1, 365)]
    public int GlobalPromotionWindowDays { get; set; } = 7;
}
