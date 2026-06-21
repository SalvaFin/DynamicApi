using System.ComponentModel.DataAnnotations;

namespace Dynamic.Fidelity.Application.DTOs.Requests;

public class ValidateTicketQrRequest
{
    [MaxLength(1024)]
    public string? QrToken { get; set; }

    [MaxLength(64)]
    public string? TicketCode { get; set; }

    [Range(typeof(decimal), "0", "999999999")]
    public decimal? PurchaseAmount { get; set; }

    [MaxLength(128)]
    public string? StoreReference { get; set; }
}
