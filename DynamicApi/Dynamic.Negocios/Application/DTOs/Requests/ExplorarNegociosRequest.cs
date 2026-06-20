namespace Dynamic.Negocios.Application.DTOs.Requests;

public class ExplorarNegociosRequest
{
    public string? Search { get; set; }
    public string? Q { get; set; }
    public decimal? Latitud { get; set; }
    public decimal? Longitud { get; set; }
    public int Page { get; set; } = 1;
    public int? PageSize { get; set; }
}
