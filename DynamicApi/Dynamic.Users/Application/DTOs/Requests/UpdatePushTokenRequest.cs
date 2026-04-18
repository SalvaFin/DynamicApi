using System.ComponentModel.DataAnnotations;
using Dynamic.Users.Domain.Enums;

namespace Dynamic.Users.Application.DTOs.Requests;

public class UpdatePushTokenRequest
{
    [MaxLength(1024)]
    public string? PushToken { get; set; }

    public PushProvider PushProvider { get; set; } = PushProvider.None;

    public bool NotificationsEnabled { get; set; } = true;

    [MaxLength(128)]
    public string? DeviceId { get; set; }

    [MaxLength(128)]
    public string? InstallationId { get; set; }

    [MaxLength(256)]
    public string? DeviceFingerprint { get; set; }

    [MaxLength(64)]
    public string? AppVersion { get; set; }

    [MaxLength(64)]
    public string? AppBuild { get; set; }
}
