using System.ComponentModel.DataAnnotations;

namespace Dynamic.Reports.Application.Options;

public sealed class DynamicReportsDatabaseOptions
{
    public const string SectionName = "DynamicReportsDatabase";

    [Required]
    public string ConnectionStringName { get; set; } = "DefaultConnection";

    [Required]
    public string MariaDbVersion { get; set; } = "11.4.0";
}
