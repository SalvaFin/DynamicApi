using Dynamic.Fidelity.Domain.Entities;

namespace Dynamic.Fidelity.Application.Contracts.Services;

public interface ITicketEventPublisher
{
    Task PublishReceivedAsync(Ticket ticket, string source, CancellationToken cancellationToken = default);
    Task PublishRejectedAsync(Ticket ticket, string message, CancellationToken cancellationToken = default);
    Task PublishUsedAsync(Ticket ticket, Guid validatedByUserId, DateTime usedAtUtc, CancellationToken cancellationToken = default);
}
