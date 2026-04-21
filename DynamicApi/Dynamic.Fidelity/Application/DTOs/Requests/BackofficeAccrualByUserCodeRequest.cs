using System.ComponentModel.DataAnnotations;

namespace Dynamic.Fidelity.Application.DTOs.Requests;

public class BackofficeAccrualByUserCodeRequest
{
    [Required]
    [MaxLength(32)]
    public string UserCode { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "999999999")]
    public decimal AmountEuros { get; set; }

    [MaxLength(512)]
    public string? Reason { get; set; }

    [MaxLength(256)]
    public string? Reference { get; set; }
}
