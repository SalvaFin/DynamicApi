using Dynamic.Fidelity.Domain.Enums;

namespace Dynamic.Fidelity.Domain.Entities;

public class Ticket
{
    public Guid Id { get; set; }
    public Guid NegocioId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? ParentTicketId { get; set; }
    public Guid? SourceQrCampaignId { get; set; }
    public Guid? SourcePromotionCampaignId { get; set; }
    public Guid? SourcePromotionRecipientId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public TipoTicket Tipo { get; set; } = TipoTicket.Libre;
    public CategoriaEnvioTicket CategoriaEnvioEspecial { get; set; } = CategoriaEnvioTicket.General;
    public decimal? Valor { get; set; }
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
    public int? PuntosCoste { get; set; }
    public int? MaxUsosPorCliente { get; set; }
    public int UsosConsumidos { get; set; }
    public int? ValidezDiasDesdeAsignacion { get; set; }
    public bool RequiereValidacionManual { get; set; } = true;
    public bool EsDeUnSoloUso { get; set; } = true;
    public bool EsPlantilla { get; set; }
    public bool Activo { get; set; } = true;
    public bool Publicado { get; set; }
    public bool Usado { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? AvailableFromUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public string? UsedInStoreReference { get; set; }
    public string? UsedByEmployeeReference { get; set; }
    public string? NotasInternas { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public TicketRedemption RedeemWelcomeAtRegistration(Guid employeeUserId, DateTime now)
    {
        if (employeeUserId == Guid.Empty)
            throw new ArgumentException("El trabajador responsable es obligatorio.", nameof(employeeUserId));
        if (Id == Guid.Empty || NegocioId == Guid.Empty || !UserId.HasValue || UserId == Guid.Empty ||
            EsPlantilla || CategoriaEnvioEspecial != CategoriaEnvioTicket.PrimerRegistro ||
            Usado || UsosConsumidos != 0)
            throw new InvalidOperationException("Solo se puede registrar así un bono de bienvenida recién asignado.");

        // The benefit has already been delivered in the store: it must not remain redeemable,
        // even if the template was configured for multiple uses.
        Usado = true;
        UsosConsumidos = 1;
        UsedAtUtc = now;
        UsedByEmployeeReference = employeeUserId.ToString("D");
        UsedInStoreReference = "Bono de bienvenida disfrutado durante el alta";
        UpdatedAtUtc = now;

        return new TicketRedemption
        {
            Id = Guid.NewGuid(), TicketId = Id, NegocioId = NegocioId, UserId = UserId.Value,
            ValidatedByUserId = employeeUserId, ParentTicketId = ParentTicketId,
            SourceQrCampaignId = SourceQrCampaignId,
            SourcePromotionCampaignId = SourcePromotionCampaignId,
            SourcePromotionRecipientId = SourcePromotionRecipientId,
            TicketNombreSnapshot = Nombre, TicketTipoSnapshot = Tipo.ToString(),
            TicketCategoriaSnapshot = CategoriaEnvioEspecial.ToString(), TicketCodeSnapshot = CodigoVisible,
            UsageNumber = UsosConsumidos, StoreReference = UsedInStoreReference, CreatedAtUtc = now
        };
    }
}
