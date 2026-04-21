using Dynamic.Fidelity.Domain.Entities;

namespace Dynamic.Fidelity.Application.Contracts.Repositories;

public interface IPointsTransactionRepository
{
    Task<IReadOnlyCollection<PointsTransaction>> GetByUserAndNegocioAsync(Guid userId, Guid negocioId, CancellationToken cancellationToken = default);
    Task AddAsync(PointsTransaction transaction, CancellationToken cancellationToken = default);
}
