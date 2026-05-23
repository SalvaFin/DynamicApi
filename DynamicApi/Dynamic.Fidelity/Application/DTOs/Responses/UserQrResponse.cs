namespace Dynamic.Fidelity.Application.DTOs.Responses;

public class UserQrResponse
{
    public Guid UserId { get; set; }
    public string UserCode { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string QrSvg { get; set; } = string.Empty;
}
