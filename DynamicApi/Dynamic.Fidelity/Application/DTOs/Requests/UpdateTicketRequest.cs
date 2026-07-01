using System.ComponentModel.DataAnnotations;
using Dynamic.Fidelity.Domain.Enums;

namespace Dynamic.Fidelity.Application.DTOs.Requests;

public class UpdateTicketRequest
{
    [Required]
    [MaxLength(180)]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Descripcion { get; set; }

    public TipoTicket Tipo { get; set; } = TipoTicket.Libre;
    public CategoriaEnvioTicket CategoriaEnvioEspecial { get; set; } = CategoriaEnvioTicket.General;

    public decimal? Valor { get; set; }
    public int? PuntosCoste { get; set; }

    public int? MaxUsosPorCliente { get; set; }
    public int? ValidezDiasDesdeAsignacion { get; set; }

    public bool Activo { get; set; } = true;
    public bool Publicado { get; set; }
    public bool EsDeUnSoloUso { get; set; } = true;
    public bool RequiereValidacionManual { get; set; } = true;
    public DateTime? AvailableFromUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}
