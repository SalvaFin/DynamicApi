using Dynamic.Negocios.Domain.Enums;

namespace Dynamic.Negocios.Application.DTOs.Responses;

public class NegocioVinculadoResponse
{
    public Guid VinculacionId { get; set; }
    public Guid NegocioId { get; set; }
    public string NombreComercial { get; set; } = string.Empty;
    public string SlugPortal { get; set; } = string.Empty;
    public string? LogoPrincipalUrl { get; set; }
    public string? ImagenHeroUrl { get; set; }
    public string? ColorPrimario { get; set; }
    public string? ColorSecundario { get; set; }
    public bool NegocioActivo { get; set; }
    public bool PortalPublicado { get; set; }
    public TipoVinculacionNegocioUsuario TipoVinculacion { get; set; }
    public string? TituloRelacion { get; set; }
    public bool EsPrincipal { get; set; }
    public bool PuedeAccederBackoffice { get; set; }
    public bool PuedeGestionarNegocio { get; set; }
    public bool PuedeGestionarClientes { get; set; }
    public bool PuedeGestionarCampanas { get; set; }
    public bool PuedeGestionarPuntos { get; set; }
    public bool PuedeValidarTickets { get; set; }
    public bool PuedeVerReportes { get; set; }
    public DateTime? FechaInicioUtc { get; set; }
    public DateTime? FechaFinUtc { get; set; }
    public DateTime FechaVinculacionUtc { get; set; }
}
