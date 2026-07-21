using System.ComponentModel.DataAnnotations;

namespace Dynamic.Promotions.Application.Options;

public class PromotionEmailOptions
{
    public const string SectionName = "Promotions:Email";

    [Required]
    public string AppBaseUrl { get; set; } = "https://appdynamic.es";

    [Required]
    public string PublicApiBaseUrl { get; set; } = "https://appdynamic.es";

    public string CompanyName { get; set; } = "Dynamic";
    public string? CompanyAddress { get; set; }
}
