namespace Dynamic.Fidelity.Application.DTOs.Responses;

public class FormarParteNegocioResponse
{
    public Guid NegocioId { get; set; }
    public Guid AudienciaId { get; set; }
    public bool YaFormabaParte { get; set; }
    public bool FormadoAhora { get; set; }
    public bool EsFavorito { get; set; }
    public bool BonoBienvenidaRecibido { get; set; }
}
