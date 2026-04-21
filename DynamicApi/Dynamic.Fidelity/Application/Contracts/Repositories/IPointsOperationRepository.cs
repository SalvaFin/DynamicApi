using Dynamic.Fidelity.Domain.Entities;

namespace Dynamic.Fidelity.Application.Contracts.Repositories;

public interface IPointsOperationRepository
{
    Task<PointsOperation?> GetByIdAsync(Guid operationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<PointsOperation>> GetFailedOrCancelledByNegocioAsync(Guid negocioId, CancellationToken cancellationToken = default);
    Task AddAsync(PointsOperation operation, CancellationToken cancellationToken = default);
    void Update(PointsOperation operation);
}
