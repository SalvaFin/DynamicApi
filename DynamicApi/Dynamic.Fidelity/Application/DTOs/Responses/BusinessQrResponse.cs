namespace Dynamic.Fidelity.Application.DTOs.Responses;

public class BusinessQrResponse
{
    public Guid NegocioId { get; set; }
    public string SlugPortal { get; set; } = string.Empty;
    public string PublicUrl { get; set; } = string.Empty;
    public string QrSvg { get; set; } = string.Empty;
    public string QrDataUrl { get; set; } = string.Empty;
    public string ImageFormat { get; set; } = "svg";
}
