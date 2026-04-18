namespace Dynamic.Users.Domain.Entities;

public class UserSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? UserDeviceId { get; set; }
    public string JwtId { get; set; } = string.Empty;
    public string RefreshTokenHash { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? AppName { get; set; }
    public string? AppVersion { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastSeenAtUtc { get; set; }
    public DateTime RefreshTokenExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? RevocationReason { get; set; }

    public UserAccount User { get; set; } = null!;
    public UserDevice? UserDevice { get; set; }
}
