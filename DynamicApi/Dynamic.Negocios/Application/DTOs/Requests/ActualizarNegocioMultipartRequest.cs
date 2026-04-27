using Microsoft.AspNetCore.Http;

namespace Dynamic.Negocios.Application.DTOs.Requests;

public class ActualizarNegocioMultipartRequest : ActualizarNegocioRequest
{
    public IFormFile? LogoPrincipalFile { get; set; }
    public IFormFile? LogoSecundarioFile { get; set; }
    public IFormFile? IconoFile { get; set; }
    public IFormFile? ImagenHeroFile { get; set; }
    public IFormFile? ImagenCoverFile { get; set; }
    public IFormFile? ImagenMobileFile { get; set; }
    public List<IFormFile>? GaleriaImagenesFiles { get; set; }
}
