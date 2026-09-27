using Dynamic.Fidelity.Domain.Entities;

namespace Dynamic.Fidelity.Application.Models;

public sealed record WelcomeTicketClaimResult(Ticket? Ticket, bool AlreadyClaimed);
