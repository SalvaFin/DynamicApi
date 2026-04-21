using System.ComponentModel.DataAnnotations;

namespace Dynamic.Fidelity.Application.DTOs.Requests;

public class BackofficeAccrualByUserCodeRequest
{
    [Required]
    [MaxLength(32)]
    public string UserCode { get; set; } = string.Empty;

    public decimal AmountEuros { get; set; }

    [MaxLength(512)]
    public string? Reason { get; set; }

    [MaxLength(256)]
    public string? Reference { get; set; }
}
