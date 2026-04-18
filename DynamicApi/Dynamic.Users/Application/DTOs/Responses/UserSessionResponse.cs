using Dynamic.Users.Domain.Enums;

namespace Dynamic.Users.Application.DTOs.Responses;

public class UserSessionResponse
{
    public Guid SessionId { get; set; }
    public Guid? DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public DeviceType DeviceType { get; set; }
    public DevicePlatform Platform { get; set; }
    public string? AppName { get; set; }
    public string? AppVersion { get; set; }
    public bool NotificationsEnabled { get; set; }
    public PushProvider PushProvider { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastSeenAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public bool IsCurrent { get; set; }
}
