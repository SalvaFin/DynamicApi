using Dynamic.Fidelity.Application.Contracts.Repositories;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dynamic.Fidelity.Infrastructure.Repositories;

public class PointsTransactionRepository : IPointsTransactionRepository
{
    private readonly DynamicFidelityDbContext _dbContext;

    public PointsTransactionRepository(DynamicFidelityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<PointsTransaction>> GetByUserAndNegocioAsync(Guid userId, Guid negocioId, CancellationToken cancellationToken = default)
        => await _dbContext.PointsTransactions
            .Where(transaction => transaction.UserId == userId && transaction.NegocioId == negocioId)
            .OrderByDescending(transaction => transaction.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public Task AddAsync(PointsTransaction transaction, CancellationToken cancellationToken = default)
        => _dbContext.PointsTransactions.AddAsync(transaction, cancellationToken).AsTask();
}
