namespace Dynamic.Promotions.Application.DTOs.Responses;

public sealed class UnseenPromotionsResponse
{
    public IReadOnlyCollection<ReceivedPromotionResponse> Items { get; set; } = [];
    public int TotalPending { get; set; }
}

public sealed class PresentedPromotionsResponse
{
    public int PresentedCount { get; set; }
    public DateTime PresentedAtUtc { get; set; }
}
