namespace Dynamic.Fidelity.Application.DTOs.Responses;

public class ValidatedTicketResponse : TicketResponse
{
    public Guid? SourceQrCampaignId { get; set; }
    public string? CodigoInterno { get; set; }
    public string? CodigoVisible { get; set; }
    public string? TituloCanje { get; set; }
    public string? InstruccionesCanje { get; set; }
    public string? CondicionesUso { get; set; }
    public string? MensajeMarketing { get; set; }
    public decimal? DescuentoPorcentaje { get; set; }
    public decimal? DescuentoImporteFijo { get; set; }
    public string? BeneficioEspecialResumen { get; set; }
    public string? BeneficioEspecialDetalle { get; set; }
    public decimal? GastoMinimoRequerido { get; set; }
    public string? UsedInStoreReference { get; set; }
    public string? UsedByEmployeeReference { get; set; }
}
