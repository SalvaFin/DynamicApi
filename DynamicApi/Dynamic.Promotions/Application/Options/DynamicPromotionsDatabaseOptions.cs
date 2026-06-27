using System.ComponentModel.DataAnnotations;

namespace Dynamic.Promotions.Application.Options;

public class DynamicPromotionsDatabaseOptions
{
    public const string SectionName = "DynamicPromotionsDatabase";

    [Required]
    public string ConnectionStringName { get; set; } = "DefaultConnection";

    [Required]
    public string MariaDbVersion { get; set; } = "11.4.0";
}
