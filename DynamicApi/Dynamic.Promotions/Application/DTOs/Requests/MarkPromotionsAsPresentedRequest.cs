namespace Dynamic.Promotions.Application.DTOs.Requests;

public sealed class MarkPromotionsAsPresentedRequest
{
    public IReadOnlyCollection<Guid>? RecipientIds { get; set; }
}
