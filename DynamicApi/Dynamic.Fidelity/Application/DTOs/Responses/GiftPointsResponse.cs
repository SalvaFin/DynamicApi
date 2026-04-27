namespace Dynamic.Fidelity.Application.DTOs.Responses;

public class GiftPointsResponse
{
    public Guid NegocioId { get; set; }
    public Guid SenderUserId { get; set; }
    public Guid RecipientUserId { get; set; }
    public int PointsTransferred { get; set; }
    public int SenderBalanceAfter { get; set; }
    public int RecipientBalanceAfter { get; set; }
    public bool RecipientWasLinked { get; set; }
    public bool RecipientReceivedWelcomeTicket { get; set; }
    public bool SenderReceivedReferralTicket { get; set; }
    public string? RecipientUserCode { get; set; }
    public string Message { get; set; } = string.Empty;
}
