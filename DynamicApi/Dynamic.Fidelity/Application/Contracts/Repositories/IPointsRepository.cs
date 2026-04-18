using Dynamic.Fidelity.Domain.Entities;

namespace Dynamic.Fidelity.Application.Contracts.Repositories;

public interface IPointsRepository
{
    Task<Points?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Points?> GetByUserAndNegocioAsync(Guid userId, Guid negocioId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Points>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Points>> GetByNegocioIdAsync(Guid negocioId, CancellationToken cancellationToken = default);
    Task AddAsync(Points points, CancellationToken cancellationToken = default);
    void Update(Points points);
}
