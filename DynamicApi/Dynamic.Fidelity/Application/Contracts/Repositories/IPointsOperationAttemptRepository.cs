using Dynamic.Fidelity.Domain.Entities;

namespace Dynamic.Fidelity.Application.Contracts.Repositories;

public interface IPointsOperationAttemptRepository
{
    Task<IReadOnlyCollection<PointsOperationAttempt>> GetByNegocioAsync(Guid negocioId, CancellationToken cancellationToken = default);
    Task AddAsync(PointsOperationAttempt attempt, CancellationToken cancellationToken = default);
}
