namespace Dynamic.Fidelity.Application.DTOs.Responses;

public class AssignedTicketQrResponse
{
    public Guid NegocioId { get; set; }
    public Guid TicketId { get; set; }
    public Guid UserId { get; set; }
    public string? TicketCode { get; set; }
    public string QrToken { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string QrSvg { get; set; } = string.Empty;
    public string ImageFormat { get; set; } = "svg";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}
