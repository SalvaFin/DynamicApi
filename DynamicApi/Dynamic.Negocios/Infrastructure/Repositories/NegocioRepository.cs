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

    public Task<Negocio?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Negocios.FirstOrDefaultAsync(negocio => negocio.Id == id && !negocio.IsDeleted, cancellationToken);

    public Task<Negocio?> GetBySlugAsync(string slugPortal, CancellationToken cancellationToken = default)
        => _dbContext.Negocios.FirstOrDefaultAsync(negocio => negocio.SlugPortal == slugPortal && !negocio.IsDeleted, cancellationToken);

    public Task AddAsync(Negocio negocio, CancellationToken cancellationToken = default)
        => _dbContext.Negocios.AddAsync(negocio, cancellationToken).AsTask();

    public void Update(Negocio negocio)
        => _dbContext.Negocios.Update(negocio);
}
