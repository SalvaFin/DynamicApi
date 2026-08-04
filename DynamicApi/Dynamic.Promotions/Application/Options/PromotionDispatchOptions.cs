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

    [Range(1, 500)]
    public int EmailBatchSize { get; set; } = 20;

    [Range(1, 3600)]
    public int EmailsPerMinute { get; set; } = 60;

    [Range(1, 20)]
    public int MaxEmailAttempts { get; set; } = 5;

    [Range(2, 300)]
    public int EmailTelemetryRefreshSeconds { get; set; } = 10;
}
