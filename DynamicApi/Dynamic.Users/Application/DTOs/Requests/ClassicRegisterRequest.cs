using System.ComponentModel.DataAnnotations;

namespace Dynamic.Users.Application.DTOs.Requests;

public class ClassicRegisterRequest
{
    [Required]
    [MaxLength(64)]
    public string UserName { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Email { get; set; }

    [MaxLength(32)]
    public string? PhoneNumber { get; set; }

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public string ConfirmPassword { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? FirstName { get; set; }

    [MaxLength(128)]
    public string? LastName { get; set; }
}
