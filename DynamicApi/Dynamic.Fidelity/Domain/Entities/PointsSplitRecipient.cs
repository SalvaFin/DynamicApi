namespace Dynamic.Fidelity.Domain.Entities;

public class PointsSplitRecipient
{
    public Guid Id { get; set; }
    public Guid SplitId { get; set; }
    public Guid UserId { get; set; }
    public int PointsAmount { get; set; }
    public Guid IncomingTransactionId { get; set; }
    public PointsSplit Split { get; set; } = null!;
}
