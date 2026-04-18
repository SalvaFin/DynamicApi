using System.ComponentModel.DataAnnotations;

namespace Dynamic.Fidelity.Application.Options;

public class DynamicFidelityDatabaseOptions
{
    public const string SectionName = "DynamicFidelityDatabase";

    [Required]
    public string ConnectionStringName { get; set; } = "DefaultConnection";

    [Required]
    public string MariaDbVersion { get; set; } = "11.4.0";
}
