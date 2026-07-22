using Dynamic.Reports.Domain.Enums;

namespace Dynamic.Reports.Domain.Entities;

public sealed class SupportReport
{
    public Guid Id { get; set; }
    public Guid ReporterUserId { get; set; }
    public ReportCategory Category { get; set; }
    public ReportStatus Status { get; set; } = ReportStatus.Open;
    public ReportPriority Priority { get; set; } = ReportPriority.Normal;
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? TicketId { get; set; }
    public Guid? BusinessId { get; set; }
    public Guid? PromotionCampaignId { get; set; }
    public DateTime? OccurredAtUtc { get; set; }
    public string? PageUrl { get; set; }
    public string? AppVersion { get; set; }
    public Guid? AssignedAdminUserId { get; set; }
    public Guid? ResolvedByAdminUserId { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<ReportEvent> Events { get; set; } = [];
}
