using Dynamic.Fidelity.Application.DTOs.Responses;
using Dynamic.Promotions.Application.DTOs.Requests;
using Dynamic.Promotions.Domain.Enums;

namespace Dynamic.Promotions.Application.DTOs.Responses;

public class PromotionCampaignResponse
{
    public Guid Id { get; set; }
    public Guid NegocioId { get; set; }
    public string NegocioName { get; set; } = string.Empty;
    public Guid TicketTemplateId { get; set; }
    public TicketResponse Ticket { get; set; } = new();
    public PromotionCampaignStatus Status { get; set; }
    public int AudienceCount { get; set; }
    public int PushEligibleCount { get; set; }
    public int PushDeliveredCount { get; set; }
    public int PushFailedCount { get; set; }
    public bool PushEnabled { get; set; }
    public PromotionAudienceFiltersRequest Filters { get; set; } = new();
    public DateTime StartsAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime ScheduledAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? AudienceProcessedAtUtc { get; set; }
    public string? LastError { get; set; }
}
