using Dynamic.Negocios.Domain.Entities;

namespace Dynamic.Negocios.Application.Contracts.Repositories;

public interface INegocioUsuarioVinculacionRepository
{
    Task<NegocioUsuarioVinculacion?> GetByNegocioAndUserAsync(Guid negocioId, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<NegocioUsuarioVinculacion>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(NegocioUsuarioVinculacion vinculacion, CancellationToken cancellationToken = default);
    void Update(NegocioUsuarioVinculacion vinculacion);
}
