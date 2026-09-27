using System.ComponentModel.DataAnnotations;
using Dynamic.Users.Domain.Enums;
using Dynamic.Negocios.Domain.Enums;

namespace Dynamic.Users.Application.DTOs.Requests;

public sealed class UpdateBusinessManagedUserRequest
{
    [Required, MaxLength(64)]
    public string UserName { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Email { get; set; }

    [MaxLength(32)]
    public string? PhoneNumber { get; set; }

    [MaxLength(128)]
    public string? FirstName { get; set; }

    [MaxLength(128)]
    public string? LastName { get; set; }

    [Required]
    public UserStatus Status { get; set; }

    public TipoVinculacionNegocioUsuario? TipoVinculacion { get; set; }

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
