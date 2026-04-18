using Dynamic.Fidelity.Application.Contracts.Repositories;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dynamic.Fidelity.Infrastructure.Repositories;

public class PendingTicketAssignmentRepository : IPendingTicketAssignmentRepository
{
    private readonly DynamicFidelityDbContext _dbContext;

    public PendingTicketAssignmentRepository(DynamicFidelityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<PendingTicketAssignment?> GetByUserAndCampaignAsync(Guid userId, Guid qrCampaignId, CancellationToken cancellationToken = default)
        => _dbContext.PendingTicketAssignments.FirstOrDefaultAsync(
            assignment => assignment.UserId == userId && assignment.QrCampaignId == qrCampaignId,
            cancellationToken);

    public async Task<IReadOnlyCollection<PendingTicketAssignment>> GetPendingByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => await _dbContext.PendingTicketAssignments
            .Where(assignment => assignment.UserId == userId && !assignment.Activated)
            .ToListAsync(cancellationToken);

    public Task AddAsync(PendingTicketAssignment assignment, CancellationToken cancellationToken = default)
        => _dbContext.PendingTicketAssignments.AddAsync(assignment, cancellationToken).AsTask();

    public void Update(PendingTicketAssignment assignment)
        => _dbContext.PendingTicketAssignments.Update(assignment);
}
