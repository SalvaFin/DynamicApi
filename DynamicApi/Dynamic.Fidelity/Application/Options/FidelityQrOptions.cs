using System.ComponentModel.DataAnnotations;

namespace Dynamic.Fidelity.Application.Options;

public class FidelityQrOptions
{
    public const string SectionName = "FidelityQr";

    [Required]
    public string PublicBaseUrl { get; set; } = "https://app.tudominio.com";

    [Required]
    public string BusinessLandingPathTemplate { get; set; } = "/negocio/{slug}";

    [Required]
    public string QrQueryParameterName { get; set; } = "qr";
}
