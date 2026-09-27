namespace Dynamic.Fidelity.Domain.Entities;

public class PointsSplit
{
    public Guid Id { get; set; }
    public Guid SourceTransactionId { get; set; }
    public Guid OwnerUserId { get; set; }
    public Guid NegocioId { get; set; }
    public int OriginalPoints { get; set; }
    public int OwnerShare { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public ICollection<PointsSplitRecipient> Recipients { get; set; } = [];
}
