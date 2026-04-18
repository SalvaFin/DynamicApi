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

    [Range(1, 1440)]
    public int AccessTokenExpirationMinutes { get; set; } = 30;

    [Range(1, 365)]
    public int RefreshTokenExpirationDays { get; set; } = 30;
}
