namespace Dynamic.Fidelity.Application.DTOs.Responses;

public class UserPortalBusinessResponse
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? IconoUrl { get; set; }
    public string? ImagenCoverUrl { get; set; }
    public string? Categoria { get; set; }
    public string? Ciudad { get; set; }
    public string? Provincia { get; set; }
    public bool Activo { get; set; }
    public bool PublicadoPortal { get; set; }
    public int PuntosActuales { get; set; }
    public int TicketsActivos { get; set; }
    public int TicketsTotales { get; set; }
    public bool LinkedFromTickets { get; set; }
    public bool LinkedFromPoints { get; set; }
    public bool LinkedFromVinculacion { get; set; }
    public string? TipoVinculacion { get; set; }
    public DateTime? FechaUltimaActividadUtc { get; set; }
}
