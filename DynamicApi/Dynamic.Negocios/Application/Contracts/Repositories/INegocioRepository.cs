using Dynamic.Negocios.Domain.Entities;

namespace Dynamic.Negocios.Application.Contracts.Repositories;

public interface INegocioRepository
{
    Task<IReadOnlyCollection<Negocio>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Negocio>> ExploreAsync(IReadOnlyCollection<string> searchTerms, CancellationToken cancellationToken = default);
    Task<Negocio?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Negocio?> GetBySlugAsync(string slugPortal, CancellationToken cancellationToken = default);
    Task<Negocio?> GetByPublicIdentifierAsync(string publicIdentifier, CancellationToken cancellationToken = default);
    Task AddAsync(Negocio negocio, CancellationToken cancellationToken = default);
    void Update(Negocio negocio);
}
