using System.ComponentModel.DataAnnotations;

namespace Dynamic.Users.Application.Options;

public class DynamicUsersDatabaseOptions
{
    public const string SectionName = "DynamicUsersDatabase";

    [Required]
    public string ConnectionStringName { get; set; } = "DefaultConnection";

    [Required]
    public string MariaDbVersion { get; set; } = "11.4.0";
}
