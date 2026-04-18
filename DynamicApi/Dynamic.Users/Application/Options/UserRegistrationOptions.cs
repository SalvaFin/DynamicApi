using System.ComponentModel.DataAnnotations;

namespace Dynamic.Users.Application.Options;

public class UserRegistrationOptions
{
    public const string SectionName = "UserRegistration";

    [Required]
    public string CompletionUrlBase { get; set; } = "https://app.tudominio.com/register/complete";

    [Range(1, 240)]
    public int ValidationTokenExpirationHours { get; set; } = 48;

    [Range(0, 120)]
    public int MinimumAge { get; set; } = 16;

    public string? ClassicRegisterBootstrapKey { get; set; }
}
