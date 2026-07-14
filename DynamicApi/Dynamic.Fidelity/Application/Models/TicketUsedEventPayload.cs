namespace Dynamic.Fidelity.Application.Models;

public class TicketUsedEventPayload
{
    public Guid UserId { get; set; }
    public Guid NegocioId { get; set; }
    public Guid TicketId { get; set; }
    public Guid ValidatedByUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int UsageNumber { get; set; }
    public bool FullyUsed { get; set; }
    public DateTime UsedAtUtc { get; set; }
}
