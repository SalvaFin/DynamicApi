using Dynamic.Users.Domain.Enums;

namespace Dynamic.Users.Domain.Entities;

public class UserDevice
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? ExternalDeviceId { get; set; }
    public string? InstallationId { get; set; }
    public string? DeviceFingerprint { get; set; }
    public string? DeviceName { get; set; }
    public DeviceType DeviceType { get; set; } = DeviceType.Unknown;
    public DevicePlatform Platform { get; set; } = DevicePlatform.Unknown;
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? OperatingSystem { get; set; }
    public string? OperatingSystemVersion { get; set; }
    public string? BrowserName { get; set; }
    public string? BrowserVersion { get; set; }
    public string? AppName { get; set; }
    public string? AppVersion { get; set; }
    public string? AppBuild { get; set; }
    public string? Locale { get; set; }
    public string? TimeZone { get; set; }
    public string? PushToken { get; set; }
    public PushProvider PushProvider { get; set; } = PushProvider.None;
    public bool NotificationsEnabled { get; set; }
    public DateTime? PushTokenUpdatedAtUtc { get; set; }
    public DateTime FirstSeenAtUtc { get; set; }
    public DateTime LastSeenAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public UserAccount User { get; set; } = null!;
    public ICollection<UserSession> Sessions { get; set; } = [];
}
