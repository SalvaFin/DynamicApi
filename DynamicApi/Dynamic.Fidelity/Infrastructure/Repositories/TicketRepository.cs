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

    public async Task<IReadOnlyCollection<Ticket>> GetTemplatesByNegocioAsync(Guid negocioId, CancellationToken cancellationToken = default)
        => await _dbContext.Tickets
            .Where(ticket => ticket.NegocioId == negocioId && ticket.EsPlantilla && ticket.UserId == null)
            .OrderByDescending(ticket => ticket.UpdatedAtUtc)
            .ThenByDescending(ticket => ticket.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public Task<int> CountAssignedToUserByTemplateAsync(Guid userId, Guid templateTicketId, CancellationToken cancellationToken = default)
        => _dbContext.Tickets.CountAsync(
            ticket => ticket.UserId == userId && ticket.ParentTicketId == templateTicketId,
            cancellationToken);

    public Task AddAsync(Ticket ticket, CancellationToken cancellationToken = default)
        => _dbContext.Tickets.AddAsync(ticket, cancellationToken).AsTask();

    public void Update(Ticket ticket)
        => _dbContext.Tickets.Update(ticket);

    public void Remove(Ticket ticket)
        => _dbContext.Tickets.Remove(ticket);
}
