using System.ComponentModel.DataAnnotations;

namespace Dynamic.Fidelity.Application.Options;

public class FidelityQrOptions
{
    public const string SectionName = "FidelityQr";

    [Required]
    public string PublicBaseUrl { get; set; } = "https://app.tudominio.com";

    [Required]
    public string BusinessLandingPathTemplate { get; set; } = "/portal/tickets";

    [Required]
    public string QrQueryParameterName { get; set; } = "qr";

    [Required]
    [MinLength(32)]
    public string TicketSigningSecret { get; set; } = "CHANGE_THIS_TICKET_QR_SIGNING_SECRET_32_CHARS";

    [Required]
    public string TicketQrQueryParameterName { get; set; } = "ticketQr";
}
