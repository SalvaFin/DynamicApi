using Dynamic.Negocios.Application.Contracts.Repositories;
using Dynamic.Negocios.Domain.Entities;
using Dynamic.Negocios.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dynamic.Negocios.Infrastructure.Repositories;

public class NegocioUsuarioVinculacionRepository : INegocioUsuarioVinculacionRepository
{
    private readonly DynamicNegociosDbContext _dbContext;

    public NegocioUsuarioVinculacionRepository(DynamicNegociosDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<NegocioUsuarioVinculacion?> GetByNegocioAndUserAsync(Guid negocioId, Guid userId, CancellationToken cancellationToken = default)
        => _dbContext.NegociosUsuariosVinculaciones
            .Include(vinculacion => vinculacion.Negocio)
            .FirstOrDefaultAsync(
                vinculacion => vinculacion.NegocioId == negocioId && vinculacion.UserId == userId,
                cancellationToken);

    public async Task<IReadOnlyCollection<NegocioUsuarioVinculacion>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => await _dbContext.NegociosUsuariosVinculaciones
            .Include(vinculacion => vinculacion.Negocio)
            .Where(vinculacion =>
                vinculacion.UserId == userId &&
                vinculacion.Activa &&
                vinculacion.Negocio != null &&
                !vinculacion.Negocio.IsDeleted)
            .OrderByDescending(vinculacion => vinculacion.EsPrincipal)
            .ThenBy(vinculacion => vinculacion.Negocio!.NombreComercial)
            .ToListAsync(cancellationToken);

    public Task AddAsync(NegocioUsuarioVinculacion vinculacion, CancellationToken cancellationToken = default)
        => _dbContext.NegociosUsuariosVinculaciones.AddAsync(vinculacion, cancellationToken).AsTask();

    public void Update(NegocioUsuarioVinculacion vinculacion)
        => _dbContext.NegociosUsuariosVinculaciones.Update(vinculacion);
}
