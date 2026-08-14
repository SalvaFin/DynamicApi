namespace Dynamic.Negocios.Domain.Entities;

public class NegocioAudiencia
{
    public Guid Id { get; set; }
    public Guid NegocioId { get; set; }
    public Guid UserId { get; set; }
    public bool Activa { get; set; } = true;
    public bool EsFavorito { get; set; }
    public bool PermiteCorreosPromocionales { get; set; } = true;
    public DateTime? CorreosPromocionalesAceptadosAtUtc { get; set; }
    public DateTime? CorreosPromocionalesRevocadosAtUtc { get; set; }
    public string? OrigenAlta { get; set; }
    public string? UltimaActividadOrigen { get; set; }
    public DateTime FechaAltaUtc { get; set; }
    public DateTime? FechaBajaUtc { get; set; }
    public DateTime UltimaActividadUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public Negocio? Negocio { get; set; }
}
