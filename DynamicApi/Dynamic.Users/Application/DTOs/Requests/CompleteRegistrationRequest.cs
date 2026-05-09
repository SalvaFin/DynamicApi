using System.ComponentModel.DataAnnotations;

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

    [Range(0, 130)]
    public int Edad { get; set; }

    public ClientDeviceContextRequest? Client { get; set; }
}
