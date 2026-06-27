namespace Dynamic.Promotions.Application.DTOs.Responses;

public class ReceivedPromotionsPageResponse
{
    public IReadOnlyCollection<ReceivedPromotionResponse> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public int UnreadCount { get; set; }
}
