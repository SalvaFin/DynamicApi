namespace Dynamic.Fidelity.Domain.Entities;

public class PointsOperationAttempt
{
    public Guid Id { get; set; }
    public Guid OperationId { get; set; }
    public Guid NegocioId { get; set; }
    public Guid UserId { get; set; }
    public Guid? AttemptedByUserId { get; set; }
    public int AttemptNumber { get; set; }
    public bool Succeeded { get; set; }
    public bool CancelledOperation { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
