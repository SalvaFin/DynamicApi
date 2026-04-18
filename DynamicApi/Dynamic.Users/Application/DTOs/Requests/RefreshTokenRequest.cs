using System.ComponentModel.DataAnnotations;

namespace Dynamic.Users.Application.DTOs.Requests;

public class RefreshTokenRequest
{
    [Required]
    [MaxLength(2048)]
    public string RefreshToken { get; set; } = string.Empty;

    public Guid? SessionId { get; set; }

    public ClientDeviceContextRequest? Client { get; set; }
}
