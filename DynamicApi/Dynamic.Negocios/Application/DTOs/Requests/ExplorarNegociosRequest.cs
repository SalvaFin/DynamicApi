using Dynamic.Negocios.Application.ModelBinding;
using Microsoft.AspNetCore.Mvc;

namespace Dynamic.Negocios.Application.DTOs.Requests;

public class ExplorarNegociosRequest
{
    public string? Search { get; set; }
    public string? Q { get; set; }
    [ModelBinder(BinderType = typeof(CoordinateModelBinder))]
    public decimal? Latitud { get; set; }
    [ModelBinder(BinderType = typeof(CoordinateModelBinder))]
    public decimal? Longitud { get; set; }
    public int Page { get; set; } = 1;
    public int? PageSize { get; set; }
}
