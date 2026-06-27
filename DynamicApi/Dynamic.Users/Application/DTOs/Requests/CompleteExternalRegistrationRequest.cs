using System.ComponentModel.DataAnnotations;

namespace Dynamic.Users.Application.DTOs.Requests;

public class CompleteExternalRegistrationRequest
{
    [Required]
    [MaxLength(32)]
    public string Provider { get; set; } = string.Empty;

    [Required]
    public string IdToken { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Nonce { get; set; }

    [Required]
    [MaxLength(128)]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string Apellidos { get; set; } = string.Empty;

    [Range(0, 130)]
    public int Edad { get; set; }

    public bool TermsAccepted { get; set; }

    public bool PrivacyPolicyAccepted { get; set; }

    public bool MarketingAccepted { get; set; }

    [MaxLength(128)]
    public string? QrToken { get; set; }

    public ClientDeviceContextRequest? Client { get; set; }
}
