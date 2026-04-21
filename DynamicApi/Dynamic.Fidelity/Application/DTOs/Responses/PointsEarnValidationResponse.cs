namespace Dynamic.Fidelity.Application.DTOs.Responses;

public class PointsEarnValidationResponse
{
    public Guid OperationId { get; set; }
    public Guid UserId { get; set; }
    public Guid NegocioId { get; set; }
    public int PointsEarned { get; set; }
    public int TotalBalance { get; set; }
    public int RemainingAttempts { get; set; }
    public Guid? ValidatorUserId { get; set; }
    public bool Cancelled { get; set; }
    public string Message { get; set; } = string.Empty;
}
