using System.ComponentModel.DataAnnotations;

namespace Dynamic.Users.Application.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = "DynamicApi";

    [Required]
    public string Audience { get; set; } = "DynamicApi.Clients";

    [Required]
    [MinLength(32)]
    public string Secret { get; set; } = string.Empty;

    [Range(1, 168)]
    public int AdminSessionExpirationHours { get; set; } = 24;

    [Range(1, 36500)]
    public int NonAdminPersistentSessionDays { get; set; } = 36500;
}
