using System.ComponentModel.DataAnnotations;

namespace Dynamic.Users.Application.Options;

public class UserRegistrationOptions
{
    public const string SectionName = "UserRegistration";

    [Required]
    public string CompletionUrlBase { get; set; } = "https://app.tudominio.com/register/complete";

    [Required]
    public string PasswordResetUrlBase { get; set; } = "https://app.tudominio.com/reset-password";

    [Range(1, 240)]
    public int ValidationTokenExpirationHours { get; set; } = 48;

    [Range(1, 24)]
    public int PasswordResetTokenExpirationHours { get; set; } = 2;

    [Range(0, 120)]
    public int MinimumAge { get; set; } = 16;

    public string? ClassicRegisterBootstrapKey { get; set; }
}
