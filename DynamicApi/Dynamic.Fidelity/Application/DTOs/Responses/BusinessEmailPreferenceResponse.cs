namespace Dynamic.Fidelity.Application.DTOs.Responses;

public class BusinessEmailPreferenceResponse
{
    public Guid NegocioId { get; set; }
    public bool PermiteCorreosPromocionales { get; set; }
    public DateTime? AceptadoAtUtc { get; set; }
    public DateTime? RevocadoAtUtc { get; set; }
}
