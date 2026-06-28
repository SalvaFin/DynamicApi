using System.ComponentModel.DataAnnotations;
using Dynamic.Fidelity.Application.DTOs.Requests;

namespace Dynamic.Promotions.Application.DTOs.Requests;

public class CreatePromotionCampaignRequest
{
    public Guid? TicketTemplateId { get; set; }
    public CreateTicketRequest? Ticket { get; set; }

    public DateTime? StartsAtUtc { get; set; }

    [Required]
    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? ScheduledAtUtc { get; set; }

    [MaxLength(128)]
    public string? IdempotencyKey { get; set; }

    public PromotionAudienceFiltersRequest Filters { get; set; } = new();
}
