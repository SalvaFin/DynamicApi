using System.ComponentModel.DataAnnotations;
using Dynamic.Reports.Domain.Enums;

namespace Dynamic.Reports.Application.DTOs.Requests;

public sealed class AdminUpdateReportRequest
{
    [EnumDataType(typeof(ReportStatus))]
    public ReportStatus? Status { get; set; }

    [EnumDataType(typeof(ReportPriority))]
    public ReportPriority? Priority { get; set; }

    public bool AssignToMe { get; set; }
    public bool Unassign { get; set; }

    [StringLength(5000, MinimumLength = 2)]
    public string? PublicReply { get; set; }

    [StringLength(5000, MinimumLength = 2)]
    public string? InternalNote { get; set; }
}
