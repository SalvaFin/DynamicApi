using Dynamic.Fidelity.Domain.Entities;

namespace Dynamic.Fidelity.Application.Contracts.Repositories;

public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Ticket ticket, CancellationToken cancellationToken = default);
}
