using System.ComponentModel.DataAnnotations;

namespace Dynamic.Users.Application.DTOs.Requests;

public class RegisterStartRequest
{
    [Required]
    [MaxLength(256)]
    public string Contact { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? QrToken { get; set; }

    public ClientDeviceContextRequest? Client { get; set; }
}
