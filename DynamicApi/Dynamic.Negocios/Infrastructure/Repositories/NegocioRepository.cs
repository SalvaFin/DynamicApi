using Dynamic.Negocios.Application.Contracts.Repositories;
using Dynamic.Negocios.Domain.Entities;
using Dynamic.Negocios.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dynamic.Negocios.Infrastructure.Repositories;

public class NegocioRepository : INegocioRepository
{
    private readonly DynamicNegociosDbContext _dbContext;

    public NegocioRepository(DynamicNegociosDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<Negocio>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _dbContext.Negocios
            .Where(negocio => !negocio.IsDeleted)
            .OrderBy(negocio => negocio.NombreComercial)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Negocio>> ExploreAsync(
        IReadOnlyCollection<string> searchTerms,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Negocio> query = _dbContext.Negocios
            .AsNoTracking()
            .Where(negocio => !negocio.IsDeleted && negocio.Activo && negocio.PublicadoPortal);

        foreach (string searchTerm in searchTerms)
        {
            string likePattern = $"%{EscapeLikePattern(searchTerm)}%";
            query = query.Where(negocio =>
                EF.Functions.Like(negocio.NombreComercial, likePattern, "\\") ||
                (negocio.Etiquetas != null && EF.Functions.Like(negocio.Etiquetas, likePattern, "\\")));
        }

        return await query
            .OrderBy(negocio => negocio.NombreComercial)
            .ToListAsync(cancellationToken);
    }

    public Task<Negocio?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Negocios.FirstOrDefaultAsync(negocio => negocio.Id == id && !negocio.IsDeleted, cancellationToken);

    public Task<Negocio?> GetBySlugAsync(string slugPortal, CancellationToken cancellationToken = default)
        => _dbContext.Negocios.FirstOrDefaultAsync(negocio => negocio.SlugPortal == slugPortal && !negocio.IsDeleted, cancellationToken);

    public Task<Negocio?> GetByPublicIdentifierAsync(string publicIdentifier, CancellationToken cancellationToken = default)
    {
        string normalizedIdentifier = NormalizePublicIdentifier(publicIdentifier);
        string routeWithLeadingSlash = $"/{normalizedIdentifier}";

        return _dbContext.Negocios.FirstOrDefaultAsync(
            negocio => !negocio.IsDeleted &&
                (negocio.SlugPortal == normalizedIdentifier ||
                 negocio.RutaPortal == normalizedIdentifier ||
                 negocio.RutaPortal == routeWithLeadingSlash),
            cancellationToken);
    }

    public Task AddAsync(Negocio negocio, CancellationToken cancellationToken = default)
        => _dbContext.Negocios.AddAsync(negocio, cancellationToken).AsTask();

    public void Update(Negocio negocio)
        => _dbContext.Negocios.Update(negocio);

    private static string EscapeLikePattern(string value)
        => value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private static string NormalizePublicIdentifier(string value)
        => value.Trim().Trim('/').ToLowerInvariant();
}
