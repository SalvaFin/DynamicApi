namespace Dynamic.Fidelity.Application.DTOs.Responses;

public class SeguirNegocioResponse
{
    public Guid NegocioId { get; set; }
    public Guid VinculacionId { get; set; }
    public bool YaEstabaVinculado { get; set; }
    public bool VinculadoAhora { get; set; }
    public bool BonoBienvenidaRecibido { get; set; }
}
