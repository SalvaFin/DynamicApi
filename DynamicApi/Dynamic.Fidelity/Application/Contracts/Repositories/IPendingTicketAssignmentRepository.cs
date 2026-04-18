using Dynamic.Fidelity.Domain.Entities;

namespace Dynamic.Fidelity.Application.Contracts.Repositories;

public interface IPendingTicketAssignmentRepository
{
    Task<PendingTicketAssignment?> GetByUserAndCampaignAsync(Guid userId, Guid qrCampaignId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<PendingTicketAssignment>> GetPendingByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(PendingTicketAssignment assignment, CancellationToken cancellationToken = default);
    void Update(PendingTicketAssignment assignment);
}
