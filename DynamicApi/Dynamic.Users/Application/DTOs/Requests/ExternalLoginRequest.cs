using System.ComponentModel.DataAnnotations;

namespace Dynamic.Users.Application.DTOs.Requests;

public class ExternalLoginRequest
{
    [Required]
    [MaxLength(32)]
    public string Provider { get; set; } = string.Empty;

    [Required]
    public string IdToken { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Nonce { get; set; }

    [MaxLength(128)]
    public string? FirstName { get; set; }

    [MaxLength(128)]
    public string? LastName { get; set; }

    [MaxLength(128)]
    public string? DisplayName { get; set; }

    [MaxLength(128)]
    public string? QrToken { get; set; }

    public ClientDeviceContextRequest? Client { get; set; }
}
