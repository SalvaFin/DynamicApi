using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Dynamic.Users.Application.Common;
using Dynamic.Users.Domain.Enums;

namespace Dynamic.Users.Application.DTOs.Requests;

public class CompleteRegistrationRequest
{
    [Required]
    [MaxLength(256)]
    public string Contact { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string ValidationToken { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string Apellidos { get; set; } = string.Empty;

    public DateTime? BirthDate { get; set; }

    [JsonConverter(typeof(UserGenderJsonConverter))]
    public UserGender Gender { get; set; } = UserGender.OtroPrefieroNoEspecificar;

    [MaxLength(24)]
    public string? PostalCode { get; set; }

    [Required]
    public SpanishProvince? Province { get; set; }

    public bool TermsAccepted { get; set; }

    public bool PrivacyPolicyAccepted { get; set; }

    public bool MarketingAccepted { get; set; }

    public ClientDeviceContextRequest? Client { get; set; }
}
