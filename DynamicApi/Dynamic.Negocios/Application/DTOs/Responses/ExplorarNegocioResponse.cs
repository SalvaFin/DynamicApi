namespace Dynamic.Negocios.Application.DTOs.Responses;

public class ExplorarNegocioResponse
{
    public Guid Id { get; set; }
    public string NombreComercial { get; set; } = string.Empty;
    public string SlugPortal { get; set; } = string.Empty;
    public string? CategoriaPrincipal { get; set; }
    public string? Subcategoria { get; set; }
    public string? Etiquetas { get; set; }
    public string? DescripcionCorta { get; set; }
    public string? DireccionLinea1 { get; set; }
    public string? CodigoPostal { get; set; }
    public string? Ciudad { get; set; }
    public string? Provincia { get; set; }
    public string? PaisCodigoIso2 { get; set; }
    public decimal? Latitud { get; set; }
    public decimal? Longitud { get; set; }
    public double? DistanciaKm { get; set; }
    public int TicketsDados { get; set; }
    public int TicketsUsados { get; set; }
    public int ActividadDynamic { get; set; }
    public string? LogoPrincipalUrl { get; set; }
    public string? IconoUrl { get; set; }
    public string? ImagenCoverUrl { get; set; }
    public string? ImagenMobileUrl { get; set; }
    public string? NombreProgramaFidelizacion { get; set; }
    public string? DescripcionProgramaFidelizacion { get; set; }
    public decimal? RatioConversionEurosAPuntos { get; set; }
    public int? PuntosBienvenida { get; set; }
    public decimal? ValorMonetarioPunto { get; set; }
    public bool PermiteRegistroPublico { get; set; }
}
