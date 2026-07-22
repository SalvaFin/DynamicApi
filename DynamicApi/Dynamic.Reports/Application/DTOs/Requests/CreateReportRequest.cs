using System.ComponentModel.DataAnnotations;
using Dynamic.Reports.Domain.Enums;

namespace Dynamic.Reports.Application.DTOs.Requests;

public sealed class CreateReportRequest
{
    [EnumDataType(typeof(ReportCategory))]
    public ReportCategory Category { get; set; }

    [Required, StringLength(160, MinimumLength = 5)]
    public string Subject { get; set; } = string.Empty;

    [Required, StringLength(5000, MinimumLength = 10)]
    public string Description { get; set; } = string.Empty;

    public Guid? TicketId { get; set; }
    public Guid? BusinessId { get; set; }
    public Guid? PromotionCampaignId { get; set; }
    public DateTime? OccurredAtUtc { get; set; }

    [StringLength(1000)]
    public string? PageUrl { get; set; }

    [StringLength(64)]
    public string? AppVersion { get; set; }
}
