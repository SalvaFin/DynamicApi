using System.ComponentModel.DataAnnotations;

namespace Dynamic.Fidelity.Application.Options;

public class FidelityQrOptions
{
    public const string SectionName = "FidelityQr";

    [Required]
    public string PublicBaseUrl { get; set; } = "https://app.tudominio.com";

    [Required]
    public string RegisterPath { get; set; } = "/register";
}
