using Dynamic.Fidelity.Domain.Enums;

namespace Dynamic.Fidelity.Application.DTOs.Responses;

public class TicketResponse
{
    public Guid Id { get; set; }
    public Guid NegocioId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public TipoTicket Tipo { get; set; }
    public decimal Valor { get; set; }
    public bool Activo { get; set; }
    public bool Publicado { get; set; }
    public bool EsDeUnSoloUso { get; set; }
    public bool RequiereValidacionManual { get; set; }
    public bool EsPlantilla { get; set; }
    public DateTime? AvailableFromUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
