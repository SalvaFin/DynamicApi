using Dynamic.Fidelity.Application.DTOs.Responses;

namespace Dynamic.Promotions.Application.DTOs.Responses;

public class ReceivedPromotionResponse
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid TicketTemplateId { get; set; }
    public Guid? AssignedTicketId { get; set; }
    public PromotionBusinessSummaryResponse Negocio { get; set; } = new();
    public TicketResponse Ticket { get; set; } = new();
    public DateTime StartsAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAtUtc { get; set; }
    public bool IsPresented { get; set; }
    public DateTime? PresentedAtUtc { get; set; }
}

public class PromotionBusinessSummaryResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
}
