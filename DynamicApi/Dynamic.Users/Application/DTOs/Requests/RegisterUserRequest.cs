using System.ComponentModel.DataAnnotations;

namespace Dynamic.Users.Application.DTOs.Requests;

public class RegisterUserRequest
{
    [Required]
    [MaxLength(256)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(256)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? FirstName { get; set; }

    [MaxLength(128)]
    public string? LastName { get; set; }

    [MaxLength(128)]
    public string? DisplayName { get; set; }

    [MaxLength(32)]
    public string? PhoneNumber { get; set; }

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

    public bool AcceptTerms { get; set; }

    public bool AcceptPrivacyPolicy { get; set; }

    public bool AcceptMarketing { get; set; }

    public ClientDeviceContextRequest? Client { get; set; }
}
