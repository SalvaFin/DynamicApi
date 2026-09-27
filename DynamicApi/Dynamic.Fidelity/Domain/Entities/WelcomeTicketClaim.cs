namespace Dynamic.Fidelity.Domain.Entities;

public class WelcomeTicketClaim
{
    public Guid UserId { get; set; }
    public Guid NegocioId { get; set; }
    public Guid TicketId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
