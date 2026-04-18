using System.ComponentModel.DataAnnotations;
using Dynamic.Users.Domain.Enums;

namespace Dynamic.Users.Application.DTOs.Requests;

public class ClientDeviceContextRequest
{
    [MaxLength(128)]
    public string? DeviceId { get; set; }

    [MaxLength(128)]
    public string? InstallationId { get; set; }

    [MaxLength(256)]
    public string? DeviceFingerprint { get; set; }

    [MaxLength(128)]
    public string? DeviceName { get; set; }

    public DeviceType DeviceType { get; set; } = DeviceType.Web;

    public DevicePlatform Platform { get; set; } = DevicePlatform.Web;

    [MaxLength(128)]
    public string? Manufacturer { get; set; }

    [MaxLength(128)]
    public string? Model { get; set; }

    [MaxLength(64)]
    public string? OperatingSystem { get; set; }

    [MaxLength(64)]
    public string? OperatingSystemVersion { get; set; }

    [MaxLength(64)]
    public string? BrowserName { get; set; }

    [MaxLength(64)]
    public string? BrowserVersion { get; set; }

    [MaxLength(128)]
    public string? AppName { get; set; }

    [MaxLength(64)]
    public string? AppVersion { get; set; }

    [MaxLength(64)]
    public string? AppBuild { get; set; }

    [MaxLength(16)]
    public string? Locale { get; set; }

    [MaxLength(64)]
    public string? TimeZone { get; set; }

    [MaxLength(1024)]
    public string? PushToken { get; set; }

    public PushProvider PushProvider { get; set; } = PushProvider.None;

    public bool NotificationsEnabled { get; set; }
}
