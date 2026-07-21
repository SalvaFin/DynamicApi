namespace Dynamic.Promotions.Application.Models;

public sealed record PromotionEmailQueueSnapshot(
    DateTime ObservedAtUtc,
    string Status,
    string StatusReason,
    string Scope,
    string InstanceId,
    DateTime ProcessStartedAtUtc,
    DateTime? WorkerLastHeartbeatAtUtc,
    DateTime? QueueLastSampledAtUtc,
    DateTime? LastProgressAtUtc,
    bool SmtpEnabled,
    PromotionEmailQueueConfiguration Configuration,
    PromotionEmailQueueDepth Queue,
    PromotionEmailQueueRuntime Runtime,
    PromotionEmailCurrentDelivery? CurrentDelivery,
    IReadOnlyCollection<PromotionEmailActiveCampaign> ActiveCampaigns,
    IReadOnlyCollection<PromotionEmailRecentError> RecentErrors,
    IReadOnlyCollection<string> Warnings);

public sealed record PromotionEmailQueueConfiguration(
    int PollingIntervalSeconds,
    int BatchSize,
    int EmailsPerMinute,
    int MaxAttempts,
    int TelemetryRefreshSeconds,
    int StalledAfterSeconds);

public sealed record PromotionEmailQueueDepth(
    long Pending,
    long Ready,
    long Scheduled,
    long Blocked,
    long Processing,
    long Failed,
    long StaleProcessing,
    DateTime? OldestReadyAtUtc,
    double? OldestReadyAgeSeconds,
    DateTime? EstimatedDrainAtUtc);

public sealed record PromotionEmailQueueRuntime(
    long AttemptedSinceStart,
    long DeliveredSinceStart,
    long RetriedSinceStart,
    long FailedSinceStart,
    long SkippedSinceStart,
    long RecoveredStaleLeasesSinceStart,
    int ConsecutiveErrors,
    int DeliveredLastMinute,
    decimal AverageDeliveredPerMinuteLastFiveMinutes,
    DateTime? LastDeliveredAtUtc,
    DateTime? LastErrorAtUtc);

public sealed record PromotionEmailCurrentDelivery(
    Guid DeliveryId,
    Guid CampaignId,
    Guid NegocioId,
    string BusinessName,
    string PromotionName,
    int Attempt,
    DateTime StartedAtUtc);

public sealed record PromotionEmailActiveCampaign(
    Guid CampaignId,
    Guid NegocioId,
    string BusinessName,
    string PromotionName,
    long Total,
    long Pending,
    long Processing,
    long Delivered,
    long Failed,
    decimal ProgressPercentage,
    DateTime? OldestPendingAtUtc,
    DateTime ExpiresAtUtc);

public sealed record PromotionEmailRecentError(
    DateTime OccurredAtUtc,
    Guid? DeliveryId,
    Guid? CampaignId,
    Guid? NegocioId,
    string Category,
    string Message,
    int? Attempt,
    bool RetryScheduled);

public sealed record PromotionEmailQueueDatabaseSample(
    DateTime SampledAtUtc,
    long Pending,
    long Ready,
    long Scheduled,
    long Blocked,
    long Processing,
    long Failed,
    long StaleProcessing,
    DateTime? OldestReadyAtUtc,
    IReadOnlyCollection<PromotionEmailActiveCampaign> ActiveCampaigns);
