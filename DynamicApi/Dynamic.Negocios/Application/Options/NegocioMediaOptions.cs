using System.ComponentModel.DataAnnotations;

namespace Dynamic.Negocios.Application.Options;

public class NegocioMediaOptions
{
    public const string SectionName = "NegocioMedia";

    [Required]
    public string StorageRootPath { get; set; } = "uploads/negocios-media";

    [Required]
    public string PublicPathPrefix { get; set; } = "/negocios-media";

    public string? PublicBaseUrl { get; set; }

    [Range(1, long.MaxValue)]
    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;
}
