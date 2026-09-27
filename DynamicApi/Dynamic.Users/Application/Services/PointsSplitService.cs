using System.Data;
using Dynamic.Fidelity.Application.Models;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Domain.Enums;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Dynamic.Negocios.Infrastructure.Persistence;
using Dynamic.Notify.Application.Contracts;
using Dynamic.Notify.Application.Models;
using Dynamic.Users.Application.DTOs.Responses;
using Dynamic.Users.Domain.Entities;
using Dynamic.Users.Domain.Enums;
using Dynamic.Users.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dynamic.Users.Application.Services;

public class PointsSplitService
{
    private readonly DynamicFidelityDbContext _fidelity;
    private readonly DynamicUsersDbContext _users;
    private readonly DynamicNegociosDbContext _negocios;
    private readonly IUserEventPublisher _events;

    public PointsSplitService(
        DynamicFidelityDbContext fidelity,
        DynamicUsersDbContext users,
        DynamicNegociosDbContext negocios,
        IUserEventPublisher events)
    {
        _fidelity = fidelity;
        _users = users;
        _negocios = negocios;
        _events = events;
    }

    public async Task<SplitRecipientResponse?> ResolveByEmailAsync(string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        string normalized = email.Trim().ToUpperInvariant();
        if (normalized.Length is < 3 or > 256 || !normalized.Contains('@')) return null;

        UserAccount? user = await _users.Users.AsNoTracking()
            .FirstOrDefaultAsync(item => item.NormalizedEmail == normalized &&
                item.EmailConfirmed && item.RegistrationCompleted &&
                item.Status == UserStatus.Active && item.Role == UserRole.User,
                cancellationToken);
        return user is null ? null : ToRecipient(user, 0);
    }

    public async Task<SplitRecipientResponse?> ResolveByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        UserAccount? user = await _users.Users.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == userId && item.EmailConfirmed &&
                item.RegistrationCompleted && item.Status == UserStatus.Active &&
                item.Role == UserRole.User, cancellationToken);
        return user is null ? null : ToRecipient(user, 0);
    }

    public async Task<PointsSplitResponse?> GetAsync(Guid ownerUserId, Guid sourceTransactionId, CancellationToken cancellationToken)
    {
        PointsSplit? split = await _fidelity.PointsSplits.AsNoTracking()
            .Include(item => item.Recipients)
            .FirstOrDefaultAsync(item => item.SourceTransactionId == sourceTransactionId &&
                item.OwnerUserId == ownerUserId, cancellationToken);
        return split is null ? null : await ToResponseAsync(split, cancellationToken);
    }

    public async Task<PointsSplitStatusResponse?> GetStatusAsync(Guid ownerUserId, Guid sourceTransactionId, CancellationToken cancellationToken)
    {
        PointsTransaction? source = await _fidelity.PointsTransactions.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceTransactionId && item.UserId == ownerUserId,
                cancellationToken);
        if (source is null) return null;

        PointsSplitResponse? split = await GetAsync(ownerUserId, sourceTransactionId, cancellationToken);
        int balance = await _fidelity.Points.AsNoTracking()
            .Where(item => item.UserId == ownerUserId && item.NegocioId == source.NegocioId)
            .Select(item => item.CurrentBalance).FirstOrDefaultAsync(cancellationToken);
        bool eligible = source.TransactionType == PointsTransactionType.Earn &&
            source.OperationId.HasValue && source.PointsAmount >= 2 && split is null;
        string businessName = await _negocios.Negocios.AsNoTracking()
            .Where(item => item.Id == source.NegocioId)
            .Select(item => item.NombreComercial)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
        return new PointsSplitStatusResponse
        {
            SourceTransactionId = sourceTransactionId, NegocioId = source.NegocioId,
            BusinessName = businessName,
            OriginalPoints = source.PointsAmount, CurrentBalance = balance,
            Eligible = eligible, Split = split,
            IneligibleReason = split is not null ? "Esta compra ya se ha repartido."
                : eligible ? null : "Esta transacción no es una compra validada que admita reparto."
        };
    }

    public async Task<(PointsSplitResponse? Split, string? Error)> CreateAsync(
        Guid ownerUserId, Guid sourceTransactionId, IReadOnlyCollection<string> emails,
        CancellationToken cancellationToken)
    {
        if (emails is null || emails.Count is < 1 or > 9)
            return (null, "Selecciona entre uno y nueve amigos.");

        PointsTransaction? source = await _fidelity.PointsTransactions.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceTransactionId && item.UserId == ownerUserId,
                cancellationToken);
        if (source is null || source.TransactionType != PointsTransactionType.Earn ||
            source.OperationId is null || source.PointsAmount < 2)
            return (null, "Esta transacción no admite reparto.");

        bool businessActive = await _negocios.Negocios.AsNoTracking()
            .AnyAsync(item => item.Id == source.NegocioId && item.Activo && !item.IsDeleted,
                cancellationToken);
        if (!businessActive) return (null, "El negocio ya no está activo.");

        List<SplitRecipientResponse> recipients = [];
        foreach (string email in emails)
        {
            SplitRecipientResponse? recipient = await ResolveByEmailAsync(email, cancellationToken);
            if (recipient is null)
                return (null, "Alguno de los correos no corresponde a una cuenta de cliente activa y verificada.");
            recipients.Add(recipient);
        }

        if (recipients.Select(item => item.UserId).Distinct().Count() != recipients.Count ||
            recipients.Any(item => item.UserId == ownerUserId))
            return (null, "La lista contiene personas repetidas o incluye tu propia cuenta.");

        recipients = recipients.OrderBy(item => item.UserId.ToString("N"), StringComparer.Ordinal).ToList();
        int participantCount = recipients.Count + 1;
        if (source.PointsAmount < participantCount)
            return (null, "No hay suficientes puntos para dar al menos uno a cada persona.");

        int baseShare = source.PointsAmount / participantCount;
        int remainder = source.PointsAmount % participantCount;
        int ownerShare = baseShare + (remainder > 0 ? 1 : 0);
        for (int index = 0; index < recipients.Count; index++)
            recipients[index].PointsAmount = baseShare + (index < remainder - 1 ? 1 : 0);
        int totalDebit = source.PointsAmount - ownerShare;

        await using var transaction = await _fidelity.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            if (await _fidelity.PointsSplits.AnyAsync(item => item.SourceTransactionId == sourceTransactionId, cancellationToken))
                return (null, "Esta transacción ya se ha repartido.");

            // Lock every existing balance in a stable order; the source transaction has one owner.
            Dictionary<Guid, Points> balances = [];
            Guid[] userIds = recipients.Select(item => item.UserId).Append(ownerUserId).Order().ToArray();
            foreach (Guid userId in userIds)
            {
                List<Points> locked = await _fidelity.Points
                    .FromSqlInterpolated($"SELECT * FROM fidelity_points WHERE UserId = {userId} AND NegocioId = {source.NegocioId} FOR UPDATE")
                    .ToListAsync(cancellationToken);
                Points? points = locked.FirstOrDefault();
                if (points is not null) balances[userId] = points;
            }

            if (!balances.TryGetValue(ownerUserId, out Points? ownerPoints) ||
                ownerPoints.CurrentBalance < totalDebit)
                return (null, "Tu saldo actual no alcanza para entregar las partes de tus amigos.");
            if (ownerPoints.TotalSpent > int.MaxValue - totalDebit)
                return (null, "No se puede registrar este movimiento de puntos.");

            foreach (SplitRecipientResponse recipient in recipients)
            {
                if (balances.TryGetValue(recipient.UserId, out Points? existing) &&
                    (existing.CurrentBalance > int.MaxValue - recipient.PointsAmount ||
                     existing.TotalEarned > int.MaxValue - recipient.PointsAmount))
                    return (null, "Alguno de los saldos no admite más puntos.");
            }

            DateTime now = DateTime.UtcNow;
            PointsSplit split = new()
            {
                Id = Guid.NewGuid(), SourceTransactionId = sourceTransactionId,
                OwnerUserId = ownerUserId, NegocioId = source.NegocioId,
                OriginalPoints = source.PointsAmount, OwnerShare = ownerShare, CreatedAtUtc = now
            };
            _fidelity.PointsSplits.Add(split);

            ownerPoints.CurrentBalance -= totalDebit;
            ownerPoints.TotalSpent += totalDebit;
            ownerPoints.LastSpentAtUtc = now;
            ownerPoints.LastMovementAtUtc = now;
            ownerPoints.LastReason = "Reparto de puntos";
            ownerPoints.LastReference = $"split:{split.Id:N}";
            ownerPoints.UpdatedAtUtc = now;
            _fidelity.PointsTransactions.Add(new PointsTransaction
            {
                Id = Guid.NewGuid(), UserId = ownerUserId, NegocioId = source.NegocioId,
                PointsId = ownerPoints.Id, TransactionType = PointsTransactionType.SplitOut,
                PointsAmount = totalDebit, BalanceBefore = ownerPoints.CurrentBalance + totalDebit,
                BalanceAfter = ownerPoints.CurrentBalance, Reason = "Reparto de puntos",
                Reference = $"split:{split.Id:N}", CreatedAtUtc = now
            });

            foreach (SplitRecipientResponse recipient in recipients)
            {
                if (!balances.TryGetValue(recipient.UserId, out Points? points))
                {
                    points = new Points
                    {
                        Id = Guid.NewGuid(), UserId = recipient.UserId, NegocioId = source.NegocioId,
                        CreatedAtUtc = now, UpdatedAtUtc = now
                    };
                    _fidelity.Points.Add(points);
                }

                int before = points.CurrentBalance;
                points.CurrentBalance += recipient.PointsAmount;
                points.TotalEarned += recipient.PointsAmount;
                points.LastEarnedAtUtc = now;
                points.LastMovementAtUtc = now;
                points.LastReason = "Puntos recibidos en un reparto";
                points.LastReference = $"split:{split.Id:N}";
                points.UpdatedAtUtc = now;
                Guid incomingId = Guid.NewGuid();
                _fidelity.PointsTransactions.Add(new PointsTransaction
                {
                    Id = incomingId, UserId = recipient.UserId, NegocioId = source.NegocioId,
                    PointsId = points.Id, CounterpartyUserId = ownerUserId,
                    TransactionType = PointsTransactionType.SplitIn,
                    PointsAmount = recipient.PointsAmount, BalanceBefore = before,
                    BalanceAfter = points.CurrentBalance, Reason = "Puntos recibidos en un reparto",
                    Reference = $"split:{split.Id:N}", CreatedAtUtc = now
                });
                split.Recipients.Add(new PointsSplitRecipient
                {
                    Id = Guid.NewGuid(), SplitId = split.Id, UserId = recipient.UserId,
                    PointsAmount = recipient.PointsAmount, IncomingTransactionId = incomingId
                });

                // All module contexts use the same database. Link inside this transaction,
                // without creating a referral ticket or implying promotional email consent.
                await _fidelity.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO negocio_audience_memberships
                    (Id, NegocioId, UserId, Activa, EsFavorito, PermiteCorreosPromocionales,
                     OrigenAlta, UltimaActividadOrigen, FechaAltaUtc, UltimaActividadUtc,
                     CreatedAtUtc, UpdatedAtUtc)
                    VALUES ({Guid.NewGuid()}, {source.NegocioId}, {recipient.UserId}, 1, 0, 0,
                            {"points_split"}, {"points_split"}, {now}, {now}, {now}, {now})
                    ON DUPLICATE KEY UPDATE Activa = 1, FechaBajaUtc = NULL,
                        UltimaActividadOrigen = {"points_split"}, UltimaActividadUtc = {now}, UpdatedAtUtc = {now}
                    """, cancellationToken);
            }

            await _fidelity.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            foreach (SplitRecipientResponse recipient in recipients)
            {
                Points points = balances.GetValueOrDefault(recipient.UserId)
                    ?? _fidelity.Points.Local.First(item => item.UserId == recipient.UserId && item.NegocioId == source.NegocioId);
                await _events.PublishAsync(recipient.UserId, new UserAppEvent
                {
                    Type = "fidelity.points.received",
                    OccurredAtUtc = now,
                    Payload = new PointsReceivedEventPayload
                    {
                        UserId = recipient.UserId, NegocioId = source.NegocioId,
                        TransactionId = split.Recipients.First(item => item.UserId == recipient.UserId).IncomingTransactionId,
                        CounterpartyUserId = ownerUserId, PointsAmount = recipient.PointsAmount,
                        BalanceBefore = points.CurrentBalance - recipient.PointsAmount,
                        BalanceAfter = points.CurrentBalance, TransactionType = PointsTransactionType.SplitIn.ToString(),
                        Reason = "Puntos recibidos en un reparto", Reference = $"split:{split.Id:N}",
                        CreatedAtUtc = now
                    }
                }, cancellationToken);
            }

            return (await ToResponseAsync(split, cancellationToken), null);
        }
        catch (DbUpdateConcurrencyException)
        {
            return (null, "El saldo ha cambiado. Actualiza la pantalla y vuelve a intentarlo.");
        }
        catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase) == true)
        {
            return (null, "El reparto o alguno de los saldos ha cambiado. Actualiza la pantalla.");
        }
    }

    private async Task<PointsSplitResponse> ToResponseAsync(PointsSplit split, CancellationToken cancellationToken)
    {
        Guid[] ids = split.Recipients.Select(item => item.UserId).ToArray();
        Dictionary<Guid, UserAccount> users = await _users.Users.AsNoTracking()
            .Where(item => ids.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        return new PointsSplitResponse
        {
            Id = split.Id, SourceTransactionId = split.SourceTransactionId,
            NegocioId = split.NegocioId, OriginalPoints = split.OriginalPoints,
            OwnerShare = split.OwnerShare, CreatedAtUtc = split.CreatedAtUtc,
            Recipients = split.Recipients.OrderBy(item => item.UserId).Select(item =>
                users.TryGetValue(item.UserId, out UserAccount? user)
                    ? ToRecipient(user, item.PointsAmount)
                    : new SplitRecipientResponse { UserId = item.UserId, PointsAmount = item.PointsAmount })
                .ToList()
        };
    }

    private static SplitRecipientResponse ToRecipient(UserAccount user, int points)
        => new()
        {
            UserId = user.Id, Email = user.Email ?? string.Empty,
            DisplayName = user.DisplayName ?? user.FirstName ?? user.UserName,
            PointsAmount = points
        };
}
