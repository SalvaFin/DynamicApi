using Dynamic.Reports.Domain.Enums;

namespace Dynamic.Reports.Domain.Entities;

public sealed class ReportEvent
{
    public Guid Id { get; set; }
    public Guid ReportId { get; set; }
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

    public SupportReport Report { get; set; } = null!;
}
