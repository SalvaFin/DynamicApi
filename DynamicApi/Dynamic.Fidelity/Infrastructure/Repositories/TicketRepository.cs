using Dynamic.Fidelity.Application.Contracts.Repositories;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dynamic.Fidelity.Infrastructure.Repositories;

public class TicketRepository : ITicketRepository
{
    private readonly DynamicFidelityDbContext _dbContext;

    public TicketRepository(DynamicFidelityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Tickets.FirstOrDefaultAsync(ticket => ticket.Id == id, cancellationToken);

    public Task AddAsync(Ticket ticket, CancellationToken cancellationToken = default)
        => _dbContext.Tickets.AddAsync(ticket, cancellationToken).AsTask();
}
