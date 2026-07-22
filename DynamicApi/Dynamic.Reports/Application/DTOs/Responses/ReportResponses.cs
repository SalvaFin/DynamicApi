using Dynamic.Reports.Domain.Enums;

namespace Dynamic.Reports.Application.DTOs.Responses;

public sealed class ReportOptionsResponse
{
    public IReadOnlyCollection<ReportCategoryOptionResponse> Categories { get; set; } = [];
    public IReadOnlyCollection<ReportStatus> Statuses { get; set; } = [];
    public IReadOnlyCollection<ReportPriority> Priorities { get; set; } = [];
}

public sealed class ReportCategoryOptionResponse
{
    public ReportCategory Value { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool SupportsTicket { get; set; }
    public bool SupportsBusiness { get; set; }
    public bool SupportsPromotion { get; set; }
}

public sealed class PaginatedReportResponse<T>
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public IReadOnlyCollection<T> Items { get; set; } = [];
}

public class ReportSummaryResponse
{
    public Guid Id { get; set; }
    public Guid ReporterUserId { get; set; }
    public ReportCategory Category { get; set; }
    public ReportStatus Status { get; set; }
    public ReportPriority Priority { get; set; }
    public string Subject { get; set; } = string.Empty;
    public Guid? TicketId { get; set; }
    public Guid? BusinessId { get; set; }
    public Guid? PromotionCampaignId { get; set; }
    public Guid? AssignedAdminUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
}

public sealed class ReportDetailResponse : ReportSummaryResponse
{
    public string Description { get; set; } = string.Empty;
    public DateTime? OccurredAtUtc { get; set; }
    public string? PageUrl { get; set; }
    public string? AppVersion { get; set; }
    public Guid? ResolvedByAdminUserId { get; set; }
    public ReportUserResponse? Reporter { get; set; }
    public ReportUserResponse? AssignedAdmin { get; set; }
    public ReportReferenceResponse? Ticket { get; set; }
    public ReportReferenceResponse? Business { get; set; }
    public ReportReferenceResponse? Promotion { get; set; }
    public IReadOnlyCollection<ReportEventResponse> Timeline { get; set; } = [];
}

public sealed class ReportUserResponse
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
}

public sealed class ReportReferenceResponse
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
}

public sealed class ReportEventResponse
{
    public Guid Id { get; set; }
    public Guid ActorUserId { get; set; }
    public ReportEventKind Kind { get; set; }
    public bool IsInternal { get; set; }
    public string? Message { get; set; }
    public ReportStatus? PreviousStatus { get; set; }
    public ReportStatus? NewStatus { get; set; }
    public ReportPriority? PreviousPriority { get; set; }
    public ReportPriority? NewPriority { get; set; }
    public Guid? PreviousAssignedAdminUserId { get; set; }
    public Guid? NewAssignedAdminUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class AdminReportDashboardResponse
{
    public int Open { get; set; }
    public int InReview { get; set; }
    public int WaitingForUser { get; set; }
    public int Resolved { get; set; }
    public int Rejected { get; set; }
    public int Unassigned { get; set; }
    public int Critical { get; set; }
}
