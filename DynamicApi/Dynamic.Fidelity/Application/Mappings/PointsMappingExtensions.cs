using Dynamic.Fidelity.Application.DTOs.Responses;
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

    public static PointsTransactionResponse ToResponse(this PointsTransaction transaction)
        => new()
        {
            TransactionId = transaction.Id,
            UserId = transaction.UserId,
            NegocioId = transaction.NegocioId,
            OperationId = transaction.OperationId,
            ValidatorUserId = transaction.ValidatorUserId,
            CounterpartyUserId = transaction.CounterpartyUserId,
            TransactionType = transaction.TransactionType,
            AmountEuros = transaction.AmountEuros,
            PointsAmount = transaction.PointsAmount,
            BalanceBefore = transaction.BalanceBefore,
            BalanceAfter = transaction.BalanceAfter,
            UserCodeSnapshot = transaction.UserCodeSnapshot,
            CounterpartyUserCodeSnapshot = transaction.CounterpartyUserCodeSnapshot,
            Reason = transaction.Reason,
            Reference = transaction.Reference,
            CreatedAtUtc = transaction.CreatedAtUtc
        };

    public static PointsFailedAttemptResponse ToResponse(this PointsOperationAttempt attempt)
        => new()
        {
            AttemptId = attempt.Id,
            OperationId = attempt.OperationId,
            UserId = attempt.UserId,
            NegocioId = attempt.NegocioId,
            AttemptedByUserId = attempt.AttemptedByUserId,
            AttemptNumber = attempt.AttemptNumber,
            Succeeded = attempt.Succeeded,
            CancelledOperation = attempt.CancelledOperation,
            FailureReason = attempt.FailureReason,
            CreatedAtUtc = attempt.CreatedAtUtc
        };
}
