namespace Dynamic.Negocios.Application.DTOs.Responses;

public class ExplorarNegociosResponse
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public bool OrdenadoPorProximidad { get; set; }
    public bool OrdenadoPorDynamic { get; set; }
    public bool CoordenadasEntradaIntercambiadas { get; set; }
    public IReadOnlyCollection<ExplorarNegocioResponse> Items { get; set; } = [];
}
