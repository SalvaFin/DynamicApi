using System.ComponentModel.DataAnnotations;

namespace Dynamic.Fidelity.Application.DTOs.Requests;

public class ScanTicketQrRequest
{
    [Required]
    [MaxLength(128)]
    public string QrToken { get; set; } = string.Empty;
}
