using Dynamic.Fidelity.Application.Models;
using Dynamic.Fidelity.Domain.Entities;

namespace Dynamic.Fidelity.Application.Mappings;

public static class PointsMappingExtensions
{
    public static PointsSummary ToSummary(this Points points)
        => new()
        {
            Id = points.Id,
            UserId = points.UserId,
            NegocioId = points.NegocioId,
            CurrentBalance = points.CurrentBalance,
            TotalEarned = points.TotalEarned,
            TotalSpent = points.TotalSpent,
            PendingBalance = points.PendingBalance,
            ExpiredBalance = points.ExpiredBalance,
            LastEarnedAtUtc = points.LastEarnedAtUtc,
            LastSpentAtUtc = points.LastSpentAtUtc,
            LastMovementAtUtc = points.LastMovementAtUtc,
            LastReason = points.LastReason,
            LastReference = points.LastReference,
            CreatedAtUtc = points.CreatedAtUtc,
            UpdatedAtUtc = points.UpdatedAtUtc
        };
}
