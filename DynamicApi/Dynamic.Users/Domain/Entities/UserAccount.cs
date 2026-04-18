using Dynamic.Users.Domain.Enums;

namespace Dynamic.Users.Domain.Entities;

public class UserAccount
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string NormalizedUserName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? DisplayName { get; set; }
    public string? PhoneNumber { get; set; }
    public bool EmailConfirmed { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
    public UserStatus Status { get; set; } = UserStatus.Active;
    public string? Language { get; set; }
    public string? TimeZone { get; set; }
    public string? CountryCode { get; set; }
    public string? Region { get; set; }
    public string? City { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime? BirthDate { get; set; }
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
}
