namespace Dynamic.Fidelity.Application.DTOs.Responses;

public class AudienceFavoriteResponse
{
    public Guid NegocioId { get; set; }
    public Guid AudienciaId { get; set; }
    public bool EsFavorito { get; set; }
}
