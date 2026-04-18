using Dynamic.Fidelity.Application.Common;
using Dynamic.Fidelity.Application.Contracts.Repositories;
using Dynamic.Fidelity.Application.Contracts.Services;
using Dynamic.Fidelity.Application.Mappings;
using Dynamic.Fidelity.Application.Models;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Infrastructure.Persistence;

namespace Dynamic.Fidelity.Application.Services;

public class PointsService : IPointsService
{
    private readonly DynamicFidelityDbContext _dbContext;
    private readonly IPointsRepository _pointsRepository;

    public PointsService(DynamicFidelityDbContext dbContext, IPointsRepository pointsRepository)
    {
        _dbContext = dbContext;
        _pointsRepository = pointsRepository;
    }

    public async Task<ServiceResult<PointsSummary>> GetBalanceAsync(Guid userId, Guid negocioId, CancellationToken cancellationToken = default)
    {
        Points? points = await _pointsRepository.GetByUserAndNegocioAsync(userId, negocioId, cancellationToken);
        if (points is null)
        {
            return ServiceResult<PointsSummary>.Success(new PointsSummary
            {
                UserId = userId,
                NegocioId = negocioId,
                CurrentBalance = 0,
                TotalEarned = 0,
                TotalSpent = 0,
                PendingBalance = 0,
                ExpiredBalance = 0
            });
        }

        return ServiceResult<PointsSummary>.Success(points.ToSummary());
    }

    public async Task<ServiceResult<PointsSummary>> AddPointsAsync(
        Guid userId,
        Guid negocioId,
        int amount,
        string? reason = null,
        string? reference = null,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            return ServiceResult<PointsSummary>.Failure("validation_error", "La cantidad de puntos a añadir debe ser mayor que cero.");
        }

        Points points = await GetOrCreateAsync(userId, negocioId, cancellationToken);
        DateTime now = DateTime.UtcNow;

        points.CurrentBalance += amount;
        points.TotalEarned += amount;
        points.LastEarnedAtUtc = now;
        points.LastMovementAtUtc = now;
        points.LastReason = Normalize(reason);
        points.LastReference = Normalize(reference);
        points.UpdatedAtUtc = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<PointsSummary>.Success(points.ToSummary());
    }

    public async Task<ServiceResult<PointsSummary>> SpendPointsAsync(
        Guid userId,
        Guid negocioId,
        int amount,
        string? reason = null,
        string? reference = null,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            return ServiceResult<PointsSummary>.Failure("validation_error", "La cantidad de puntos a gastar debe ser mayor que cero.");
        }

        Points points = await GetOrCreateAsync(userId, negocioId, cancellationToken);
        if (points.CurrentBalance < amount)
        {
            return ServiceResult<PointsSummary>.Failure("insufficient_balance", "El usuario no tiene suficientes puntos.");
        }

        DateTime now = DateTime.UtcNow;
        points.CurrentBalance -= amount;
        points.TotalSpent += amount;
        points.LastSpentAtUtc = now;
        points.LastMovementAtUtc = now;
        points.LastReason = Normalize(reason);
        points.LastReference = Normalize(reference);
        points.UpdatedAtUtc = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<PointsSummary>.Success(points.ToSummary());
    }

    private async Task<Points> GetOrCreateAsync(Guid userId, Guid negocioId, CancellationToken cancellationToken)
    {
        Points? existing = await _pointsRepository.GetByUserAndNegocioAsync(userId, negocioId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        DateTime now = DateTime.UtcNow;
        Points created = new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            NegocioId = negocioId,
            CurrentBalance = 0,
            TotalEarned = 0,
            TotalSpent = 0,
            PendingBalance = 0,
            ExpiredBalance = 0,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _pointsRepository.AddAsync(created, cancellationToken);
        return created;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
