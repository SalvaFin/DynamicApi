using System.ComponentModel.DataAnnotations;

namespace Dynamic.Users.Application.DTOs.Requests;

public class ChangePasswordRequest
{
    [Required]
    [MaxLength(256)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(256)]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [Compare(nameof(NewPassword))]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
