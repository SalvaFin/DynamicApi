namespace Dynamic.Fidelity.Application.DTOs.Responses;

public class PointsEarnOperationResponse
{
    public Guid OperationId { get; set; }
    public Guid UserId { get; set; }
    public Guid NegocioId { get; set; }
    public decimal AmountEuros { get; set; }
    public decimal RatioApplied { get; set; }
    public int ExpectedPoints { get; set; }
    public int RemainingAttempts { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
