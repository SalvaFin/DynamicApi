using Dynamic.Fidelity.Application.Common;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dynamic.Fidelity.Application.Services;

public sealed class PointsAccrualReasonService(DynamicFidelityDbContext db)
{
    public async Task<IReadOnlyList<PointsAccrualReason>> ListAsync(Guid negocioId,
        bool includeInactive, CancellationToken cancellationToken)
    {
        List<PointsAccrualReason> reasons = await db.PointsAccrualReasons.AsNoTracking()
            .Where(x => x.NegocioId == negocioId && (includeInactive || x.Active))
            .OrderBy(x => x.Name).ToListAsync(cancellationToken);
        if (!reasons.Any(x => x.IsDefault)) reasons.Add(PointsAccrualReason.Default(negocioId));
        return reasons.OrderByDescending(x => x.IsDefault).ThenBy(x => x.Name).ToArray();
    }

    public async Task<ServiceResult<PointsAccrualReason>> ResolveAsync(Guid negocioId, Guid? reasonId,
        CancellationToken cancellationToken)
    {
        Guid id = reasonId ?? negocioId;
        PointsAccrualReason? reason = await db.PointsAccrualReasons.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && x.NegocioId == negocioId, cancellationToken);
        reason ??= id == negocioId ? PointsAccrualReason.Default(negocioId) : null;
        return reason is { Active: true }
            ? ServiceResult<PointsAccrualReason>.Success(reason)
            : ServiceResult<PointsAccrualReason>.Failure("validation_error",
                "El motivo seleccionado no está disponible en este negocio. Actualiza los motivos y vuelve a intentarlo.");
    }

    // Call inside the existing business/customer named locks, before linking or crediting anyone.
    // Check all uses, including those made before the owner enabled the unique checkbox.
    public async Task<ServiceResult> ValidateCustomersAsync(PointsAccrualReason reason,
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        if (reason.UniquePerCustomer && await db.PointsTransactions.AsNoTracking().AnyAsync(x =>
                x.NegocioId == reason.NegocioId && userIds.Contains(x.UserId) &&
                x.AccrualReasonId == reason.Id, cancellationToken))
            return ServiceResult.Failure("conflict",
                $"El motivo «{reason.Name}» es único por cliente y ya se ha utilizado en este negocio para uno de los clientes. No se han sumado puntos.");
        return ServiceResult.Success();
    }

    public static void Stamp(PointsTransaction transaction, PointsAccrualReason reason)
    {
        transaction.Reason = reason.Name;
        transaction.AccrualReasonId = reason.Id;
        transaction.UniqueAccrualReasonId = reason.UniquePerCustomer ? reason.Id : null;
    }
}
