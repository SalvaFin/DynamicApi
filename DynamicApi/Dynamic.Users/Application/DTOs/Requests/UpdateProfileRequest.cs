using System.ComponentModel.DataAnnotations;
using Dynamic.Users.Domain.Enums;

namespace Dynamic.Users.Application.DTOs.Requests;

public class UpdateProfileRequest
{
    [MaxLength(128)]
    public string? FirstName { get; set; }

    [MaxLength(128)]
    public string? LastName { get; set; }

    [MaxLength(128)]
    public string? DisplayName { get; set; }

    public UserGender Gender { get; set; } = UserGender.OtroPrefieroNoEspecificar;

    public DateTime? BirthDate { get; set; }

    [MaxLength(16)]
    public string? Language { get; set; }

    [MaxLength(64)]
    public string? TimeZone { get; set; }

    [MaxLength(8)]
    public string? CountryCode { get; set; }

    [MaxLength(128)]
    public string? Region { get; set; }

    [MaxLength(128)]
    public string? City { get; set; }

    [MaxLength(512)]
    [Url]
    public string? AvatarUrl { get; set; }
}
