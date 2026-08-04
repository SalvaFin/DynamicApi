using Dynamic.Fidelity.Domain.Entities;

namespace Dynamic.Fidelity.Application.Contracts.Repositories;

public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Ticket?> GetAssignedByVisibleCodeAsync(Guid negocioId, string visibleCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Ticket>> GetTemplatesByNegocioAsync(Guid negocioId, CancellationToken cancellationToken = default);
    Task<int> CountAssignedToUserByTemplateAsync(Guid userId, Guid templateTicketId, CancellationToken cancellationToken = default);
    Task AddAsync(Ticket ticket, CancellationToken cancellationToken = default);
    void Update(Ticket ticket);
    void Remove(Ticket ticket);
}
