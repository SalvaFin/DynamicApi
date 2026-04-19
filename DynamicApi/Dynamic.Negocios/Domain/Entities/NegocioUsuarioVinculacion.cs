using Dynamic.Negocios.Domain.Enums;

namespace Dynamic.Negocios.Domain.Entities;

public class NegocioUsuarioVinculacion
{
    public Guid Id { get; set; }
    public Guid NegocioId { get; set; }
    public Guid UserId { get; set; }
    public TipoVinculacionNegocioUsuario TipoVinculacion { get; set; } = TipoVinculacionNegocioUsuario.Trabajador;
    public string? TituloRelacion { get; set; }
    public bool Activa { get; set; } = true;
    public bool EsPrincipal { get; set; }
    public bool PuedeAccederBackoffice { get; set; } = true;
    public bool PuedeGestionarNegocio { get; set; }
    public bool PuedeGestionarClientes { get; set; }
    public bool PuedeGestionarCampanas { get; set; }
    public bool PuedeGestionarPuntos { get; set; }
    public bool PuedeValidarTickets { get; set; }
    public bool PuedeVerReportes { get; set; }
    public string? NotasInternas { get; set; }
    public string? OrigenVinculacion { get; set; }
    public Guid? LinkedByUserId { get; set; }
    public Guid? UnlinkedByUserId { get; set; }
    public DateTime? FechaInvitacionUtc { get; set; }
    public DateTime? FechaAceptacionUtc { get; set; }
    public DateTime? FechaInicioUtc { get; set; }
    public DateTime? FechaFinUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }

    public Negocio? Negocio { get; set; }
}
