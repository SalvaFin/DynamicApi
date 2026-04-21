using Dynamic.Fidelity.Application.Contracts.Repositories;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Domain.Enums;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dynamic.Fidelity.Infrastructure.Repositories;

public class PointsOperationRepository : IPointsOperationRepository
{
    private readonly DynamicFidelityDbContext _dbContext;

    public PointsOperationRepository(DynamicFidelityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<PointsOperation?> GetByIdAsync(Guid operationId, CancellationToken cancellationToken = default)
        => _dbContext.PointsOperations.FirstOrDefaultAsync(operation => operation.Id == operationId, cancellationToken);

    public async Task<IReadOnlyCollection<PointsOperation>> GetFailedOrCancelledByNegocioAsync(Guid negocioId, CancellationToken cancellationToken = default)
        => await _dbContext.PointsOperations
            .Where(operation =>
                operation.NegocioId == negocioId &&
                (operation.ValidationAttempts > 0 || operation.Status == PointsOperationStatus.Cancelled))
            .OrderByDescending(operation => operation.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

    public Task AddAsync(PointsOperation operation, CancellationToken cancellationToken = default)
        => _dbContext.PointsOperations.AddAsync(operation, cancellationToken).AsTask();

    public void Update(PointsOperation operation)
        => _dbContext.PointsOperations.Update(operation);
}
