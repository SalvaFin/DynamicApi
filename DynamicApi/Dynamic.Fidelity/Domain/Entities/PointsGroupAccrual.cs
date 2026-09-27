namespace Dynamic.Fidelity.Domain.Entities;

public class PointsGroupAccrual
{
    public Guid Id { get; set; }
    public Guid IdempotencyKey { get; set; }
    public Guid NegocioId { get; set; }
    public Guid WorkerUserId { get; set; }
    public decimal AmountEuros { get; set; }
    public int TotalPoints { get; set; }
    public int RecipientCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public ICollection<PointsGroupAccrualRecipient> Recipients { get; set; } = [];
}

public class PointsGroupAccrualRecipient
{
    public Guid Id { get; set; }
    public Guid GroupAccrualId { get; set; }
    public Guid UserId { get; set; }
    public int ScanOrder { get; set; }
    public int PointsAssigned { get; set; }
    public Guid TransactionId { get; set; }
    public PointsGroupAccrual GroupAccrual { get; set; } = null!;
}
