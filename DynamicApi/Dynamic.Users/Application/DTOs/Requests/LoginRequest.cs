using System.ComponentModel.DataAnnotations;

namespace Dynamic.Users.Application.DTOs.Requests;

public class LoginRequest
{
    [Required]
    [MaxLength(256)]
    public string Identity { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string Password { get; set; } = string.Empty;

    public ClientDeviceContextRequest? Client { get; set; }
}
