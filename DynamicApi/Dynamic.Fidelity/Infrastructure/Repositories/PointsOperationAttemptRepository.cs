using Dynamic.Fidelity.Application.Contracts.Repositories;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dynamic.Fidelity.Infrastructure.Repositories;

public class PointsOperationAttemptRepository : IPointsOperationAttemptRepository
{
    private readonly DynamicFidelityDbContext _dbContext;

    public PointsOperationAttemptRepository(DynamicFidelityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<PointsOperationAttempt>> GetByNegocioAsync(Guid negocioId, CancellationToken cancellationToken = default)
        => await _dbContext.PointsOperationAttempts
            .Where(attempt => attempt.NegocioId == negocioId)
            .OrderByDescending(attempt => attempt.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public Task AddAsync(PointsOperationAttempt attempt, CancellationToken cancellationToken = default)
        => _dbContext.PointsOperationAttempts.AddAsync(attempt, cancellationToken).AsTask();
}
