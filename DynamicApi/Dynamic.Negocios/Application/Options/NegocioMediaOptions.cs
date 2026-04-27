using System.ComponentModel.DataAnnotations;

namespace Dynamic.Negocios.Application.Options;

public class NegocioMediaOptions
{
    public const string SectionName = "NegocioMedia";
    public const string PublicRequestPath = "/negocios-media";

    [Required]
    public string StorageRootPath { get; set; } = "uploads/negocios-media";

    [Range(1, long.MaxValue)]
    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;
}
