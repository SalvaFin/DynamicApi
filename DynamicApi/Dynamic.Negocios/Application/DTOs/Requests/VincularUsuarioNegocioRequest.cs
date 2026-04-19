using System.ComponentModel.DataAnnotations;
using Dynamic.Negocios.Domain.Enums;

namespace Dynamic.Negocios.Application.DTOs.Requests;

public class VincularUsuarioNegocioRequest
{
    public TipoVinculacionNegocioUsuario TipoVinculacion { get; set; } = TipoVinculacionNegocioUsuario.Trabajador;

    [MaxLength(128)]
    public string? TituloRelacion { get; set; }

    public bool EsPrincipal { get; set; }
    public bool PuedeAccederBackoffice { get; set; } = true;
    public bool PuedeGestionarNegocio { get; set; }
    public bool PuedeGestionarClientes { get; set; }
    public bool PuedeGestionarCampanas { get; set; }
    public bool PuedeGestionarPuntos { get; set; }
    public bool PuedeValidarTickets { get; set; }
    public bool PuedeVerReportes { get; set; }

    [MaxLength(2000)]
    public string? NotasInternas { get; set; }

    [MaxLength(128)]
    public string? OrigenVinculacion { get; set; }

    public DateTime? FechaInicioUtc { get; set; }
    public DateTime? FechaFinUtc { get; set; }
}
