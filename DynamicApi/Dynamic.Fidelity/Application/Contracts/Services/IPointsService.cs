using Dynamic.Fidelity.Application.Common;
using Dynamic.Fidelity.Application.Models;

namespace Dynamic.Fidelity.Application.Contracts.Services;

public interface IPointsService
{
    Task<ServiceResult<PointsSummary>> GetBalanceAsync(Guid userId, Guid negocioId, CancellationToken cancellationToken = default);
    Task<ServiceResult<PointsSummary>> AddPointsAsync(
        Guid userId,
        Guid negocioId,
        int amount,
        string? reason = null,
        string? reference = null,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<PointsSummary>> SpendPointsAsync(
        Guid userId,
        Guid negocioId,
        int amount,
        string? reason = null,
        string? reference = null,
        CancellationToken cancellationToken = default);
}
