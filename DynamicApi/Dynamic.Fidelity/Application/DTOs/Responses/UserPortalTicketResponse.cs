using Dynamic.Fidelity.Domain.Enums;

namespace Dynamic.Fidelity.Application.DTOs.Responses;

public class UserPortalTicketResponse
{
    public Guid Id { get; set; }
    public UserPortalBusinessSummaryResponse Negocio { get; set; } = new();
    public string Titulo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? Recompensa { get; set; }
    public TipoTicket Tipo { get; set; }
    public CategoriaEnvioTicket Categoria { get; set; }
    public string Estado { get; set; } = string.Empty;
    public int ProgresoActual { get; set; }
    public int? ProgresoObjetivo { get; set; }
    public decimal Valor { get; set; }
    public int? PuntosCoste { get; set; }
    public string? Code { get; set; }
    public DateTime FechaAltaUtc { get; set; }
    public DateTime? AvailableFromUtc { get; set; }
    public DateTime FechaCaducidadUtc { get; set; }
    public DateTime? FechaCanjeUtc { get; set; }
    public string? CondicionesUso { get; set; }
    public string? InstruccionesCanje { get; set; }
    public bool RequiereValidacionManual { get; set; }
    public bool EsDeUnSoloUso { get; set; }
    public bool Activo { get; set; }
    public bool Usado { get; set; }
}
