namespace Dynamic.Fidelity.Application.Models;

public class PointsSummary
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid NegocioId { get; set; }
    public int CurrentBalance { get; set; }
    public int TotalEarned { get; set; }
    public int TotalSpent { get; set; }
    public int PendingBalance { get; set; }
    public int ExpiredBalance { get; set; }
    public DateTime? LastEarnedAtUtc { get; set; }
    public DateTime? LastSpentAtUtc { get; set; }
    public DateTime? LastMovementAtUtc { get; set; }
    public string? LastReason { get; set; }
    public string? LastReference { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
