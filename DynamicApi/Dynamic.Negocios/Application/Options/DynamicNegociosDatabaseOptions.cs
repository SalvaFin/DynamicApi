using System.ComponentModel.DataAnnotations;

namespace Dynamic.Negocios.Application.Options;

public class DynamicNegociosDatabaseOptions
{
    public const string SectionName = "DynamicNegociosDatabase";

    [Required]
    public string ConnectionStringName { get; set; } = "DefaultConnection";

    [Required]
    public string MariaDbVersion { get; set; } = "11.4.0";
}
