using Dynamic.Fidelity.Application.Common;
using Dynamic.Fidelity.Application.DTOs.Requests;
using Dynamic.Fidelity.Application.DTOs.Responses;
using Dynamic.Fidelity.Application.Models;

namespace Dynamic.Fidelity.Application.Contracts.Services;

public interface IPointsService
{
    Task<ServiceResult<PointsSummary>> GetBalanceAsync(Guid userId, Guid negocioId, CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyCollection<PointsTransactionResponse>>> GetTransactionsAsync(Guid userId, Guid negocioId, CancellationToken cancellationToken = default);
    Task<ServiceResult<PointsEarnOperationResponse>> InitiateEarnOperationAsync(Guid userId, Guid negocioId, InitiatePointsEarnRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<PointsEarnValidationResponse>> ValidateEarnOperationAsync(Guid operationId, Guid validatorUserId, bool isAdmin, ValidatePointsEarnOperationRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<PointsEarnValidationResponse>> BackofficeAccrualByUserCodeAsync(Guid negocioId, Guid validatorUserId, bool isAdmin, BackofficeAccrualByUserCodeRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<PointsEarnValidationResponse>> BackofficeAccrualByUserIdAsync(Guid negocioId, Guid validatorUserId, bool isAdmin, BackofficeAccrualByUserIdRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<PointsEarnValidationResponse>> BackofficeAccrualByWorkerAsync(Guid authenticatedUserId, bool isAdmin, WorkerPointsAccrualRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyCollection<PointsFailedAttemptResponse>>> GetFailedAttemptsAsync(Guid negocioId, Guid requesterUserId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<ServiceResult<GiftPointsResponse>> GiftPointsAsync(
        Guid senderUserId,
        Guid negocioId,
        GiftPointsRequest request,
        CancellationToken cancellationToken = default);
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
