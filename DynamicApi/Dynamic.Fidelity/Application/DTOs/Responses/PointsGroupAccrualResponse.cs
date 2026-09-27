namespace Dynamic.Fidelity.Application.DTOs.Responses;

public class PointsGroupAccrualResponse
{
    public Guid Id { get; set; }
    public Guid IdempotencyKey { get; set; }
    public Guid NegocioId { get; set; }
    public Guid WorkerUserId { get; set; }
    public decimal AmountEuros { get; set; }
    public int TotalPoints { get; set; }
    public int RecipientCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public List<PointsGroupAccrualRecipientResponse> Recipients { get; set; } = [];
}

public class PointsGroupAccrualRecipientResponse
{
    public Guid UserId { get; set; }
    public int PointsAssigned { get; set; }
}
