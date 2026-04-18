using Dynamic.Fidelity.Application.Contracts.Repositories;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dynamic.Fidelity.Infrastructure.Repositories;

public class PointsRepository : IPointsRepository
{
    private readonly DynamicFidelityDbContext _dbContext;

    public PointsRepository(DynamicFidelityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Points?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Points.FirstOrDefaultAsync(points => points.Id == id, cancellationToken);

    public Task<Points?> GetByUserAndNegocioAsync(Guid userId, Guid negocioId, CancellationToken cancellationToken = default)
        => _dbContext.Points.FirstOrDefaultAsync(
            points => points.UserId == userId && points.NegocioId == negocioId,
            cancellationToken);

    public async Task<IReadOnlyCollection<Points>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => await _dbContext.Points
            .Where(points => points.UserId == userId)
            .OrderByDescending(points => points.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Points>> GetByNegocioIdAsync(Guid negocioId, CancellationToken cancellationToken = default)
        => await _dbContext.Points
            .Where(points => points.NegocioId == negocioId)
            .OrderByDescending(points => points.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

    public Task AddAsync(Points points, CancellationToken cancellationToken = default)
        => _dbContext.Points.AddAsync(points, cancellationToken).AsTask();

    public void Update(Points points)
        => _dbContext.Points.Update(points);
}
