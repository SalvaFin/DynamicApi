using Dynamic.Fidelity.Domain.Enums;

namespace Dynamic.Fidelity.Domain.Entities;

public class Ticket
{
    public Guid Id { get; set; }
    public Guid NegocioId { get; set; }
    public Guid? UserId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public TipoTicket Tipo { get; set; } = TipoTicket.Especial;
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
    public bool RequiereValidacionManual { get; set; } = true;
    public bool EsDeUnSoloUso { get; set; } = true;
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
}
