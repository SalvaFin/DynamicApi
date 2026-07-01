using Dynamic.Users.Domain.Enums;

namespace Dynamic.Users.Domain.Entities;

public class UserAccount
{
    public Guid Id { get; set; }
    public string? Email { get; set; }
    public string? NormalizedEmail { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string NormalizedUserName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool PasswordIsTemporary { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? DisplayName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? NormalizedPhoneNumber { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool PhoneNumberConfirmed { get; set; }
    public bool RegistrationCompleted { get; set; }
    public int? AgeAtRegistration { get; set; }
    public string? RegistrationValidationToken { get; set; }
    public DateTime? RegistrationValidationTokenExpiresAtUtc { get; set; }
    public DateTime? RegistrationInitiatedAtUtc { get; set; }
    public DateTime? RegistrationCompletedAtUtc { get; set; }
    public DateTime? TemporaryPasswordSentAtUtc { get; set; }
    public string? PasswordResetTokenHash { get; set; }
    public DateTime? PasswordResetTokenExpiresAtUtc { get; set; }
    public DateTime? PasswordResetRequestedAtUtc { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
    public UserStatus Status { get; set; } = UserStatus.Active;
    public string? Language { get; set; }
    public string? TimeZone { get; set; }
    public string? CountryCode { get; set; }
    public string? Region { get; set; }
    public string? PostalCode { get; set; }
    public SpanishProvince? Province { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime? BirthDate { get; set; }
    public UserGender Gender { get; set; } = UserGender.OtroPrefieroNoEspecificar;
    public bool TermsAccepted { get; set; }
    public DateTime? TermsAcceptedAtUtc { get; set; }
    public bool PrivacyPolicyAccepted { get; set; }
    public DateTime? PrivacyPolicyAcceptedAtUtc { get; set; }
    public bool MarketingAccepted { get; set; }
    public DateTime? MarketingAcceptedAtUtc { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public DateTime? LastSeenAtUtc { get; set; }
    public string? LastLoginIp { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<UserDevice> Devices { get; set; } = [];
    public ICollection<UserSession> Sessions { get; set; } = [];
    public ICollection<UserAuthEvent> AuthEvents { get; set; } = [];
    public ICollection<UserExternalLogin> ExternalLogins { get; set; } = [];
}
