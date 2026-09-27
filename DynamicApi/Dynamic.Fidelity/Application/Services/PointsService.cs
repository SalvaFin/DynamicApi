using System.Security.Cryptography;
using System.Text;
using System.Data;
using Dynamic.Fidelity.Application.Common;
using Dynamic.Fidelity.Application.Contracts.Repositories;
using Dynamic.Fidelity.Application.Contracts.Services;
using Dynamic.Fidelity.Application.DTOs.Requests;
using Dynamic.Fidelity.Application.DTOs.Responses;
using Dynamic.Fidelity.Application.Mappings;
using Dynamic.Fidelity.Application.Models;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Domain.Enums;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Dynamic.Negocios.Application.Contracts.Repositories;
using Dynamic.Negocios.Domain.Entities;
using Dynamic.Negocios.Domain.Enums;
using Dynamic.Negocios.Infrastructure.Persistence;
using Dynamic.Notify.Application.Contracts;
using Dynamic.Notify.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace Dynamic.Fidelity.Application.Services;

public class PointsService : IPointsService
{
    private readonly DynamicFidelityDbContext _dbContext;
    private readonly DynamicNegociosDbContext _negociosDbContext;
    private readonly IPointsRepository _pointsRepository;
    private readonly IPointsTransactionRepository _pointsTransactionRepository;
    private readonly IPointsOperationRepository _pointsOperationRepository;
    private readonly IPointsOperationAttemptRepository _pointsOperationAttemptRepository;
    private readonly IUserCodeDirectoryService _userCodeDirectoryService;
    private readonly INegocioRepository _negocioRepository;
    private readonly INegocioUsuarioVinculacionRepository _negocioUsuarioVinculacionRepository;
    private readonly INegocioAudienciaService _negocioAudienciaService;
    private readonly IRegistrationRewardService _registrationRewardService;
    private readonly IUserEventPublisher _userEventPublisher;
    private readonly RecurrenceEvaluationService _recurrence;

    public PointsService(
        DynamicFidelityDbContext dbContext,
        DynamicNegociosDbContext negociosDbContext,
        IPointsRepository pointsRepository,
        IPointsTransactionRepository pointsTransactionRepository,
        IPointsOperationRepository pointsOperationRepository,
        IPointsOperationAttemptRepository pointsOperationAttemptRepository,
        IUserCodeDirectoryService userCodeDirectoryService,
        INegocioRepository negocioRepository,
        INegocioUsuarioVinculacionRepository negocioUsuarioVinculacionRepository,
        INegocioAudienciaService negocioAudienciaService,
        IRegistrationRewardService registrationRewardService,
        IUserEventPublisher userEventPublisher,
        RecurrenceEvaluationService recurrence)
    {
        _dbContext = dbContext;
        _negociosDbContext = negociosDbContext;
        _pointsRepository = pointsRepository;
        _pointsTransactionRepository = pointsTransactionRepository;
        _pointsOperationRepository = pointsOperationRepository;
        _pointsOperationAttemptRepository = pointsOperationAttemptRepository;
        _userCodeDirectoryService = userCodeDirectoryService;
        _negocioRepository = negocioRepository;
        _negocioUsuarioVinculacionRepository = negocioUsuarioVinculacionRepository;
        _negocioAudienciaService = negocioAudienciaService;
        _registrationRewardService = registrationRewardService;
        _userEventPublisher = userEventPublisher;
        _recurrence = recurrence;
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

    public async Task<ServiceResult<IReadOnlyCollection<PointsTransactionResponse>>> GetTransactionsAsync(Guid userId, Guid negocioId, CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<PointsTransactionResponse> transactions =
            (await _pointsTransactionRepository.GetByUserAndNegocioAsync(userId, negocioId, cancellationToken))
            .Select(transaction => transaction.ToResponse())
            .ToArray();

        return ServiceResult<IReadOnlyCollection<PointsTransactionResponse>>.Success(transactions);
    }

    public async Task<ServiceResult<IReadOnlyCollection<PointsTransactionResponse>>> GetManagedTransactionsAsync(
        Guid negocioId, Guid userId, Guid requesterUserId, bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        ServiceResult authorization = await EnsureCanManagePointsAsync(negocioId, requesterUserId,
            isAdmin, cancellationToken);
        if (!authorization.Succeeded)
            return ServiceResult<IReadOnlyCollection<PointsTransactionResponse>>.Failure(
                authorization.ErrorCode ?? "forbidden", authorization.ErrorMessage ?? "Sin permisos.");
        return await GetTransactionsAsync(userId, negocioId, cancellationToken);
    }

    public async Task<ServiceResult<PointsEarnOperationResponse>> InitiateEarnOperationAsync(
        Guid userId,
        Guid negocioId,
        InitiatePointsEarnRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.AmountEuros <= 0)
        {
            return ServiceResult<PointsEarnOperationResponse>.Failure("validation_error", "El importe debe ser mayor que 0 €.");
        }

        Negocio? negocio = await _negocioRepository.GetByIdAsync(negocioId, cancellationToken);
        if (negocio is null || !negocio.Activo || negocio.IsDeleted)
        {
            return ServiceResult<PointsEarnOperationResponse>.Failure("not_found", "El negocio no existe o no está activo.");
        }

        if (negocio.RatioConversionEurosAPuntos is null || negocio.RatioConversionEurosAPuntos <= 0)
        {
            return ServiceResult<PointsEarnOperationResponse>.Failure("validation_error", "El negocio no tiene configurado un ratio de conversión de puntos válido.");
        }

        if (string.IsNullOrWhiteSpace(negocio.ClaveMaestraLocalHash))
        {
            return ServiceResult<PointsEarnOperationResponse>.Failure("validation_error", "El negocio no tiene configurada la clave maestra del local.");
        }

        int expectedPoints = CalculatePoints(request.AmountEuros, negocio.RatioConversionEurosAPuntos.Value);
        if (expectedPoints <= 0)
        {
            return ServiceResult<PointsEarnOperationResponse>.Failure("validation_error", "El importe indicado no genera puntos con el ratio actual del negocio.");
        }

        DateTime now = DateTime.UtcNow;
        PointsOperation operation = new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            NegocioId = negocioId,
            AmountEuros = decimal.Round(request.AmountEuros, 2, MidpointRounding.AwayFromZero),
            RatioSnapshot = negocio.RatioConversionEurosAPuntos.Value,
            ExpectedPoints = expectedPoints,
            ValidationAttempts = 0,
            MaxValidationAttempts = 5,
            Status = PointsOperationStatus.Pending,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _pointsOperationRepository.AddAsync(operation, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<PointsEarnOperationResponse>.Success(new PointsEarnOperationResponse
        {
            OperationId = operation.Id,
            UserId = userId,
            NegocioId = negocioId,
            AmountEuros = operation.AmountEuros,
            RatioApplied = operation.RatioSnapshot,
            ExpectedPoints = operation.ExpectedPoints,
            RemainingAttempts = operation.MaxValidationAttempts,
            CreatedAtUtc = operation.CreatedAtUtc
        });
    }

    public async Task<ServiceResult<PointsEarnValidationResponse>> ValidateEarnOperationAsync(
        Guid operationId,
        Guid validatorUserId,
        bool isAdmin,
        ValidatePointsEarnOperationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.MasterPin) || request.MasterPin.Length != 4 || request.MasterPin.Any(character => !char.IsDigit(character)))
        {
            return ServiceResult<PointsEarnValidationResponse>.Failure("validation_error", "La clave maestra debe tener exactamente 4 dígitos.");
        }

        PointsOperation? operation = await _pointsOperationRepository.GetByIdAsync(operationId, cancellationToken);
        if (operation is null)
        {
            return ServiceResult<PointsEarnValidationResponse>.Failure("not_found", "La operación de puntos no existe.");
        }

        if (operation.Status != PointsOperationStatus.Pending)
        {
            return ServiceResult<PointsEarnValidationResponse>.Failure("conflict", "La operación ya no está pendiente de validación.");
        }

        ServiceResult authorization = await EnsureCanManagePointsAsync(operation.NegocioId, validatorUserId, isAdmin, cancellationToken);
        if (!authorization.Succeeded)
        {
            return ServiceResult<PointsEarnValidationResponse>.Failure(authorization.ErrorCode ?? "forbidden", authorization.ErrorMessage ?? "Sin permisos.");
        }

        Negocio? negocio = await _negocioRepository.GetByIdAsync(operation.NegocioId, cancellationToken);
        if (negocio is null || string.IsNullOrWhiteSpace(negocio.ClaveMaestraLocalHash))
        {
            return ServiceResult<PointsEarnValidationResponse>.Failure("validation_error", "El negocio no tiene una clave maestra configurada.");
        }

        bool isPinValid = VerifyMasterPin(request.MasterPin, negocio.ClaveMaestraLocalHash);
        DateTime now = DateTime.UtcNow;

        if (!isPinValid)
        {
            operation.ValidationAttempts++;
            operation.UpdatedAtUtc = now;

            bool cancelled = operation.ValidationAttempts >= operation.MaxValidationAttempts;
            if (cancelled)
            {
                operation.Status = PointsOperationStatus.Cancelled;
                operation.CancelReason = "MaxFailedPinAttempts";
                operation.CancelledAtUtc = now;
            }

            await _pointsOperationAttemptRepository.AddAsync(
                new PointsOperationAttempt
                {
                    Id = Guid.NewGuid(),
                    OperationId = operation.Id,
                    NegocioId = operation.NegocioId,
                    UserId = operation.UserId,
                    AttemptedByUserId = validatorUserId,
                    AttemptNumber = operation.ValidationAttempts,
                    Succeeded = false,
                    CancelledOperation = cancelled,
                    FailureReason = cancelled
                        ? "Se ha superado el límite de intentos de la clave maestra."
                        : "Clave maestra incorrecta.",
                    CreatedAtUtc = now
                },
                cancellationToken);

            _pointsOperationRepository.Update(operation);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return ServiceResult<PointsEarnValidationResponse>.Failure(
                cancelled ? "locked" : "validation_error",
                cancelled
                    ? "Se ha cancelado la operación tras superar 5 intentos fallidos."
                    : $"Clave maestra incorrecta. Intentos restantes: {Math.Max(0, operation.MaxValidationAttempts - operation.ValidationAttempts)}.");
        }

        await using IAsyncDisposable visitLock = await _recurrence.LockAsync(operation.NegocioId,
            [operation.UserId], cancellationToken);
        await _dbContext.Entry(operation).ReloadAsync(cancellationToken);
        if (operation.Status != PointsOperationStatus.Pending)
            return ServiceResult<PointsEarnValidationResponse>.Failure("conflict", "La operación ya fue validada.");
        if (await _dbContext.PointsTransactions.AsNoTracking().AnyAsync(x =>
                x.OperationId == operation.Id, cancellationToken))
            return ServiceResult<PointsEarnValidationResponse>.Failure("conflict",
                "La operación ya tiene una transacción de puntos registrada.");
        ServiceResult<CustomerPointsLinkResult> linkResult = await EnsureCustomerLinkForPointsAsync(
            operation.NegocioId,
            operation.UserId,
            validatorUserId,
            "points_earn_validation",
            cancellationToken);
        if (!linkResult.Succeeded)
        {
            return ServiceResult<PointsEarnValidationResponse>.Failure(
                linkResult.ErrorCode ?? "validation_error",
                linkResult.ErrorMessage ?? "No se ha podido vincular al usuario con el negocio.");
        }

        now = DateTime.UtcNow;
        Guid transactionId = Guid.NewGuid();
        RecurrenceResolution recurrence = await _recurrence.ResolveAsync(operation.NegocioId,
            operation.UserId, now, transactionId, cancellationToken);
        int basePoints = operation.ExpectedPoints;
        operation.ExpectedPoints = RecurrenceEvaluationService.CalculatePoints(operation.AmountEuros,
            operation.RatioSnapshot, recurrence);
        Points points = await GetOrCreateAsync(operation.UserId, operation.NegocioId, cancellationToken);
        string userCode = await _userCodeDirectoryService.EnsureUserCodeAsync(operation.UserId, cancellationToken);
        int balanceBefore = points.CurrentBalance;
        int balanceAfter = balanceBefore + operation.ExpectedPoints;

        points.CurrentBalance = balanceAfter;
        points.TotalEarned += operation.ExpectedPoints;
        points.LastEarnedAtUtc = now;
        points.LastMovementAtUtc = now;
        points.LastReason = "Compra validada en local";
        points.LastReference = operation.Id.ToString("N");
        points.UpdatedAtUtc = now;

        PointsTransaction transaction = new()
        {
            Id = transactionId,
            UserId = operation.UserId,
            NegocioId = operation.NegocioId,
            PointsId = points.Id,
            OperationId = operation.Id,
            ValidatorUserId = validatorUserId,
            TransactionType = PointsTransactionType.Earn,
            AmountEuros = operation.AmountEuros,
            PointsAmount = operation.ExpectedPoints,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceAfter,
            UserCodeSnapshot = userCode,
            Reason = "Compra validada en local",
            Reference = operation.Id.ToString("N"),
            CreatedAtUtc = now
        };
        RecurrenceEvaluationService.Stamp(transaction, operation.RatioSnapshot, basePoints, recurrence);

        operation.ValidationAttempts++;
        operation.Status = PointsOperationStatus.Completed;
        operation.CompletedTransactionId = transaction.Id;
        operation.ValidatedByUserId = validatorUserId;
        operation.ValidatedAtUtc = now;
        operation.UpdatedAtUtc = now;

        await _pointsTransactionRepository.AddAsync(transaction, cancellationToken);
        await _pointsOperationAttemptRepository.AddAsync(
            new PointsOperationAttempt
            {
                Id = Guid.NewGuid(),
                OperationId = operation.Id,
                NegocioId = operation.NegocioId,
                UserId = operation.UserId,
                AttemptedByUserId = validatorUserId,
                AttemptNumber = operation.ValidationAttempts,
                Succeeded = true,
                CancelledOperation = false,
                CreatedAtUtc = now
            },
            cancellationToken);

        _pointsOperationRepository.Update(operation);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await PublishPointsReceivedAsync(transaction, cancellationToken);

        return ServiceResult<PointsEarnValidationResponse>.Success(new PointsEarnValidationResponse
        {
            OperationId = operation.Id,
            UserId = operation.UserId,
            NegocioId = operation.NegocioId,
            PointsEarned = operation.ExpectedPoints,
            TotalBalance = balanceAfter,
            RemainingAttempts = Math.Max(0, operation.MaxValidationAttempts - operation.ValidationAttempts),
            ValidatorUserId = validatorUserId,
            Cancelled = false,
            Message = $"Operación validada correctamente. Se han acreditado {operation.ExpectedPoints} puntos."
        });
    }

    public async Task<ServiceResult<PointsEarnValidationResponse>> BackofficeAccrualByUserCodeAsync(
        Guid negocioId,
        Guid validatorUserId,
        bool isAdmin,
        BackofficeAccrualByUserCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserCode))
        {
            return ServiceResult<PointsEarnValidationResponse>.Failure("validation_error", "El código de usuario es obligatorio.");
        }

        if (request.AmountEuros <= 0)
        {
            return ServiceResult<PointsEarnValidationResponse>.Failure("validation_error", "El importe debe ser mayor que 0 €.");
        }

        ServiceResult authorization = await EnsureCanManagePointsAsync(negocioId, validatorUserId, isAdmin, cancellationToken);
        if (!authorization.Succeeded)
        {
            return ServiceResult<PointsEarnValidationResponse>.Failure(authorization.ErrorCode ?? "forbidden", authorization.ErrorMessage ?? "Sin permisos.");
        }

        Guid? userId = await _userCodeDirectoryService.ResolveUserIdAsync(request.UserCode, cancellationToken);
        if (!userId.HasValue)
        {
            return ServiceResult<PointsEarnValidationResponse>.Failure("not_found", "No existe ningún usuario con ese código.");
        }

        Negocio? negocio = await _negocioRepository.GetByIdAsync(negocioId, cancellationToken);
        if (negocio is null || negocio.RatioConversionEurosAPuntos is null || negocio.RatioConversionEurosAPuntos <= 0)
        {
            return ServiceResult<PointsEarnValidationResponse>.Failure("validation_error", "El negocio no tiene configurado un ratio de conversión de puntos válido.");
        }

        int pointsEarned = CalculatePoints(request.AmountEuros, negocio.RatioConversionEurosAPuntos.Value);
        if (pointsEarned <= 0)
        {
            return ServiceResult<PointsEarnValidationResponse>.Failure("validation_error", "El importe indicado no genera puntos con el ratio actual del negocio.");
        }

        await using IAsyncDisposable visitLock = await _recurrence.LockAsync(negocioId,
            [userId.Value], cancellationToken);
        ServiceResult<PointsEarnValidationResponse>? replay = await CheckDirectReplayAsync(negocioId,
            userId.Value, request.AmountEuros, request.IdempotencyKey, cancellationToken);
        if (replay is not null) return replay;
        ServiceResult<CustomerPointsLinkResult> linkResult = await EnsureCustomerLinkForPointsAsync(
            negocioId,
            userId.Value,
            validatorUserId,
            "points_backoffice_accrual",
            cancellationToken);
        if (!linkResult.Succeeded)
        {
            return ServiceResult<PointsEarnValidationResponse>.Failure(
                linkResult.ErrorCode ?? "validation_error",
                linkResult.ErrorMessage ?? "No se ha podido vincular al usuario con el negocio.");
        }

        DateTime now = DateTime.UtcNow;
        Guid transactionId = Guid.NewGuid();
        RecurrenceResolution recurrence = await _recurrence.ResolveAsync(negocioId, userId.Value,
            now, transactionId, cancellationToken);
        int basePoints = pointsEarned;
        pointsEarned = RecurrenceEvaluationService.CalculatePoints(request.AmountEuros,
            negocio.RatioConversionEurosAPuntos.Value, recurrence);
        Points points = await GetOrCreateAsync(userId.Value, negocioId, cancellationToken);
        int balanceBefore = points.CurrentBalance;
        int balanceAfter = balanceBefore + pointsEarned;
        string userCode = await _userCodeDirectoryService.EnsureUserCodeAsync(userId.Value, cancellationToken);

        points.CurrentBalance = balanceAfter;
        points.TotalEarned += pointsEarned;
        points.LastEarnedAtUtc = now;
        points.LastMovementAtUtc = now;
        points.LastReason = Normalize(request.Reason) ?? "Acreditación directa de backoffice";
        points.LastReference = Normalize(request.Reference);
        points.UpdatedAtUtc = now;

        PointsTransaction transaction = new()
            {
                Id = transactionId,
                UserId = userId.Value,
                NegocioId = negocioId,
                ClientOperationId = request.IdempotencyKey == Guid.Empty ? null : request.IdempotencyKey,
                PointsId = points.Id,
                ValidatorUserId = validatorUserId,
                TransactionType = PointsTransactionType.BackofficeEarn,
                AmountEuros = decimal.Round(request.AmountEuros, 2, MidpointRounding.AwayFromZero),
                PointsAmount = pointsEarned,
                BalanceBefore = balanceBefore,
                BalanceAfter = balanceAfter,
                UserCodeSnapshot = userCode,
                Reason = Normalize(request.Reason) ?? "Acreditación directa de backoffice",
                Reference = Normalize(request.Reference),
                CreatedAtUtc = now
            };
        RecurrenceEvaluationService.Stamp(transaction, negocio.RatioConversionEurosAPuntos.Value,
            basePoints, recurrence);
        await _pointsTransactionRepository.AddAsync(transaction, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await PublishPointsReceivedAsync(
            userId.Value,
            negocioId,
            pointsEarned,
            balanceBefore,
            balanceAfter,
            PointsTransactionType.BackofficeEarn,
            Normalize(request.Reason) ?? "AcreditaciÃ³n directa de backoffice",
            Normalize(request.Reference),
            transactionId: null,
            operationId: null,
            validatorUserId: validatorUserId,
            counterpartyUserId: null,
            createdAtUtc: now,
            cancellationToken: cancellationToken);

        return ServiceResult<PointsEarnValidationResponse>.Success(new PointsEarnValidationResponse
        {
            OperationId = request.IdempotencyKey,
            UserId = userId.Value,
            NegocioId = negocioId,
            PointsEarned = pointsEarned,
            TotalBalance = balanceAfter,
            RemainingAttempts = 0,
            ValidatorUserId = validatorUserId,
            Cancelled = false,
            Message = $"Se han acreditado {pointsEarned} puntos al usuario {userCode} desde backoffice."
        });
    }

    public async Task<ServiceResult<PointsEarnValidationResponse>> BackofficeAccrualByUserIdAsync(
        Guid negocioId,
        Guid validatorUserId,
        bool isAdmin,
        BackofficeAccrualByUserIdRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.UserId == Guid.Empty)
        {
            return ServiceResult<PointsEarnValidationResponse>.Failure("validation_error", "El usuario es obligatorio.");
        }

        if (request.AmountEuros <= 0)
        {
            return ServiceResult<PointsEarnValidationResponse>.Failure("validation_error", "El importe debe ser mayor que 0.");
        }

        ServiceResult authorization = await EnsureCanManagePointsAsync(negocioId, validatorUserId, isAdmin, cancellationToken);
        if (!authorization.Succeeded)
        {
            return ServiceResult<PointsEarnValidationResponse>.Failure(authorization.ErrorCode ?? "forbidden", authorization.ErrorMessage ?? "Sin permisos.");
        }

        Negocio? negocio = await _negocioRepository.GetByIdAsync(negocioId, cancellationToken);
        if (negocio is null || negocio.RatioConversionEurosAPuntos is null || negocio.RatioConversionEurosAPuntos <= 0)
        {
            return ServiceResult<PointsEarnValidationResponse>.Failure("validation_error", "El negocio no tiene configurado un ratio de conversion de puntos valido.");
        }

        int pointsEarned = CalculatePoints(request.AmountEuros, negocio.RatioConversionEurosAPuntos.Value);
        if (pointsEarned <= 0)
        {
            return ServiceResult<PointsEarnValidationResponse>.Failure("validation_error", "El importe indicado no genera puntos con el ratio actual del negocio.");
        }

        await using IAsyncDisposable visitLock = await _recurrence.LockAsync(negocioId,
            [request.UserId], cancellationToken);
        ServiceResult<PointsEarnValidationResponse>? replay = await CheckDirectReplayAsync(negocioId,
            request.UserId, request.AmountEuros, request.IdempotencyKey, cancellationToken);
        if (replay is not null) return replay;
        ServiceResult<CustomerPointsLinkResult> linkResult = await EnsureCustomerLinkForPointsAsync(
            negocioId,
            request.UserId,
            validatorUserId,
            "points_backoffice_user_accrual",
            cancellationToken);
        if (!linkResult.Succeeded)
        {
            return ServiceResult<PointsEarnValidationResponse>.Failure(
                linkResult.ErrorCode ?? "validation_error",
                linkResult.ErrorMessage ?? "No se ha podido vincular al usuario con el negocio.");
        }

        DateTime now = DateTime.UtcNow;
        Guid transactionId = Guid.NewGuid();
        RecurrenceResolution recurrence = await _recurrence.ResolveAsync(negocioId, request.UserId,
            now, transactionId, cancellationToken);
        int basePoints = pointsEarned;
        pointsEarned = RecurrenceEvaluationService.CalculatePoints(request.AmountEuros,
            negocio.RatioConversionEurosAPuntos.Value, recurrence);
        Points points = await GetOrCreateAsync(request.UserId, negocioId, cancellationToken);
        int balanceBefore = points.CurrentBalance;
        int balanceAfter = balanceBefore + pointsEarned;
        string userCode = await _userCodeDirectoryService.EnsureUserCodeAsync(request.UserId, cancellationToken);
        string reason = Normalize(request.Reason) ?? "Acreditacion directa de trabajador";
        string reference = Normalize(request.Reference) ?? $"business-user-accrual:{Guid.NewGuid():N}";

        points.CurrentBalance = balanceAfter;
        points.TotalEarned += pointsEarned;
        points.LastEarnedAtUtc = now;
        points.LastMovementAtUtc = now;
        points.LastReason = reason;
        points.LastReference = reference;
        points.UpdatedAtUtc = now;

        PointsTransaction transaction = new()
            {
                Id = transactionId,
                UserId = request.UserId,
                NegocioId = negocioId,
                ClientOperationId = request.IdempotencyKey == Guid.Empty ? null : request.IdempotencyKey,
                PointsId = points.Id,
                ValidatorUserId = validatorUserId,
                TransactionType = PointsTransactionType.BackofficeEarn,
                AmountEuros = decimal.Round(request.AmountEuros, 2, MidpointRounding.AwayFromZero),
                PointsAmount = pointsEarned,
                BalanceBefore = balanceBefore,
                BalanceAfter = balanceAfter,
                UserCodeSnapshot = userCode,
                Reason = reason,
                Reference = reference,
                CreatedAtUtc = now
            };
        RecurrenceEvaluationService.Stamp(transaction, negocio.RatioConversionEurosAPuntos.Value,
            basePoints, recurrence);
        await _pointsTransactionRepository.AddAsync(transaction, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await PublishPointsReceivedAsync(
            request.UserId,
            negocioId,
            pointsEarned,
            balanceBefore,
            balanceAfter,
            PointsTransactionType.BackofficeEarn,
            reason,
            reference,
            transactionId: null,
            operationId: null,
            validatorUserId: validatorUserId,
            counterpartyUserId: null,
            createdAtUtc: now,
            cancellationToken: cancellationToken);

        return ServiceResult<PointsEarnValidationResponse>.Success(new PointsEarnValidationResponse
        {
            OperationId = request.IdempotencyKey,
            UserId = request.UserId,
            NegocioId = negocioId,
            PointsEarned = pointsEarned,
            TotalBalance = balanceAfter,
            RemainingAttempts = 0,
            ValidatorUserId = validatorUserId,
            Cancelled = false,
            Message = $"Se han acreditado {pointsEarned} puntos al usuario {userCode}."
        });
    }

    public async Task<ServiceResult<PointsEarnValidationResponse>> BackofficeAccrualByWorkerAsync(
        Guid authenticatedUserId,
        bool isAdmin,
        WorkerPointsAccrualRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.TrabajadorId == Guid.Empty || request.UserId == Guid.Empty)
        {
            return ServiceResult<PointsEarnValidationResponse>.Failure("validation_error", "Trabajador y usuario son obligatorios.");
        }

        if (!isAdmin && authenticatedUserId != request.TrabajadorId)
        {
            return ServiceResult<PointsEarnValidationResponse>.Failure("forbidden", "El trabajador indicado no coincide con el usuario autenticado.");
        }

        if (request.DineroGastado <= 0)
        {
            return ServiceResult<PointsEarnValidationResponse>.Failure("validation_error", "El importe gastado debe ser mayor que 0.");
        }

        IReadOnlyCollection<NegocioUsuarioVinculacion> workerLinks =
            await _negocioUsuarioVinculacionRepository.GetActiveByUserIdAsync(request.TrabajadorId, cancellationToken);

        DateTime now = DateTime.UtcNow;
        List<NegocioUsuarioVinculacion> eligibleLinks = workerLinks
            .Where(link =>
                !link.RevokedAtUtc.HasValue &&
                (!link.FechaInicioUtc.HasValue || link.FechaInicioUtc.Value <= now) &&
                (!link.FechaFinUtc.HasValue || link.FechaFinUtc.Value >= now) &&
                link.Negocio is not null &&
                link.Negocio.Activo &&
                !link.Negocio.IsDeleted &&
                (link.PuedeGestionarNegocio || link.PuedeGestionarPuntos || link.PuedeValidarTickets ||
                 link.TipoVinculacion is TipoVinculacionNegocioUsuario.Propietario or TipoVinculacionNegocioUsuario.Gerente))
            .ToList();

        if (eligibleLinks.Count == 0)
        {
            return ServiceResult<PointsEarnValidationResponse>.Failure("forbidden", "El trabajador no está vinculado a ningún negocio activo con permisos para sumar puntos.");
        }

        List<NegocioUsuarioVinculacion> principalLinks = eligibleLinks
            .Where(link => link.EsPrincipal)
            .ToList();

        NegocioUsuarioVinculacion? selectedLink = eligibleLinks.Count == 1
            ? eligibleLinks[0]
            : principalLinks.Count == 1
                ? principalLinks[0]
                : null;

        if (selectedLink is null)
        {
            return ServiceResult<PointsEarnValidationResponse>.Failure("conflict", "El trabajador tiene varios negocios activos. Usa un flujo que indique el negocio explícitamente.");
        }

        Negocio negocio = selectedLink.Negocio!;
        if (negocio.RatioConversionEurosAPuntos is null || negocio.RatioConversionEurosAPuntos <= 0)
        {
            return ServiceResult<PointsEarnValidationResponse>.Failure("validation_error", "El negocio no tiene configurado un ratio de conversión de puntos válido.");
        }

        int pointsEarned = CalculatePoints(request.DineroGastado, negocio.RatioConversionEurosAPuntos.Value);
        if (pointsEarned <= 0)
        {
            return ServiceResult<PointsEarnValidationResponse>.Failure("validation_error", "El importe indicado no genera puntos con el ratio actual del negocio.");
        }

        await using IAsyncDisposable visitLock = await _recurrence.LockAsync(negocio.Id,
            [request.UserId], cancellationToken);
        ServiceResult<PointsEarnValidationResponse>? replay = await CheckDirectReplayAsync(negocio.Id,
            request.UserId, request.DineroGastado, request.IdempotencyKey, cancellationToken);
        if (replay is not null) return replay;
        ServiceResult<CustomerPointsLinkResult> linkResult = await EnsureCustomerLinkForPointsAsync(
            negocio.Id,
            request.UserId,
            request.TrabajadorId,
            "points_worker_accrual",
            cancellationToken);
        if (!linkResult.Succeeded)
        {
            return ServiceResult<PointsEarnValidationResponse>.Failure(
                linkResult.ErrorCode ?? "validation_error",
                linkResult.ErrorMessage ?? "No se ha podido vincular al usuario con el negocio.");
        }

        now = DateTime.UtcNow;
        Guid transactionId = Guid.NewGuid();
        RecurrenceResolution recurrence = await _recurrence.ResolveAsync(negocio.Id, request.UserId,
            now, transactionId, cancellationToken);
        int basePoints = pointsEarned;
        pointsEarned = RecurrenceEvaluationService.CalculatePoints(request.DineroGastado,
            negocio.RatioConversionEurosAPuntos.Value, recurrence);
        Points points = await GetOrCreateAsync(request.UserId, negocio.Id, cancellationToken);
        int balanceBefore = points.CurrentBalance;
        int balanceAfter = balanceBefore + pointsEarned;
        string userCode = await _userCodeDirectoryService.EnsureUserCodeAsync(request.UserId, cancellationToken);
        string reference = Normalize(request.Reference) ?? $"worker-accrual:{Guid.NewGuid():N}";
        string reason = Normalize(request.Reason) ?? "Compra escaneada por trabajador";

        points.CurrentBalance = balanceAfter;
        points.TotalEarned += pointsEarned;
        points.LastEarnedAtUtc = now;
        points.LastMovementAtUtc = now;
        points.LastReason = reason;
        points.LastReference = reference;
        points.UpdatedAtUtc = now;

        PointsTransaction transaction = new()
            {
                Id = transactionId,
                UserId = request.UserId,
                NegocioId = negocio.Id,
                ClientOperationId = request.IdempotencyKey == Guid.Empty ? null : request.IdempotencyKey,
                PointsId = points.Id,
                ValidatorUserId = request.TrabajadorId,
                TransactionType = PointsTransactionType.BackofficeEarn,
                AmountEuros = decimal.Round(request.DineroGastado, 2, MidpointRounding.AwayFromZero),
                PointsAmount = pointsEarned,
                BalanceBefore = balanceBefore,
                BalanceAfter = balanceAfter,
                UserCodeSnapshot = userCode,
                Reason = reason,
                Reference = reference,
                CreatedAtUtc = now
            };
        RecurrenceEvaluationService.Stamp(transaction, negocio.RatioConversionEurosAPuntos.Value,
            basePoints, recurrence);
        await _pointsTransactionRepository.AddAsync(transaction, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await PublishPointsReceivedAsync(
            request.UserId,
            negocio.Id,
            pointsEarned,
            balanceBefore,
            balanceAfter,
            PointsTransactionType.BackofficeEarn,
            reason,
            reference,
            transactionId: null,
            operationId: null,
            validatorUserId: request.TrabajadorId,
            counterpartyUserId: null,
            createdAtUtc: now,
            cancellationToken: cancellationToken);

        return ServiceResult<PointsEarnValidationResponse>.Success(new PointsEarnValidationResponse
        {
            OperationId = request.IdempotencyKey,
            UserId = request.UserId,
            NegocioId = negocio.Id,
            PointsEarned = pointsEarned,
            TotalBalance = balanceAfter,
            RemainingAttempts = 0,
            ValidatorUserId = request.TrabajadorId,
            Cancelled = false,
            Message = $"Se han acreditado {pointsEarned} puntos al usuario {userCode}."
        });
    }

    public async Task<ServiceResult<PointsGroupAccrualResponse>> BackofficeGroupAccrualAsync(
        Guid authenticatedUserId, bool isAdmin, WorkerPointsGroupAccrualRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.WorkerUserId == Guid.Empty || request.IdempotencyKey == Guid.Empty || request.AmountEuros <= 0 ||
            request.AmountEuros > 100000m || !PointsGroupAccrualDistribution.HasUniqueRecipients(request.RecipientUserIds))
            return ServiceResult<PointsGroupAccrualResponse>.Failure("validation_error", "Importe, identificador de operación y clientes únicos válidos son obligatorios.");

        if (!isAdmin && authenticatedUserId != request.WorkerUserId)
            return ServiceResult<PointsGroupAccrualResponse>.Failure("forbidden", "El trabajador indicado no coincide con el usuario autenticado.");

        PointsGroupAccrual? previous = await _dbContext.PointsGroupAccruals.AsNoTracking()
            .Include(x => x.Recipients).SingleOrDefaultAsync(x => x.IdempotencyKey == request.IdempotencyKey, cancellationToken);
        if (previous is not null)
            return MatchesGroupRequest(previous, request)
                ? ServiceResult<PointsGroupAccrualResponse>.Success(ToGroupResponse(previous))
                : ServiceResult<PointsGroupAccrualResponse>.Failure("conflict", "El identificador de operación ya se utilizó con otros datos.");

        IReadOnlyCollection<NegocioUsuarioVinculacion> links = await _negocioUsuarioVinculacionRepository
            .GetActiveByUserIdAsync(request.WorkerUserId, cancellationToken);
        DateTime now = DateTime.UtcNow;
        List<NegocioUsuarioVinculacion> eligible = links.Where(link => !link.RevokedAtUtc.HasValue &&
            (!link.FechaInicioUtc.HasValue || link.FechaInicioUtc.Value <= now) &&
            (!link.FechaFinUtc.HasValue || link.FechaFinUtc.Value >= now) && link.Negocio is { Activo: true, IsDeleted: false } &&
            (link.PuedeGestionarNegocio || link.PuedeGestionarPuntos || link.PuedeValidarTickets ||
             link.TipoVinculacion is TipoVinculacionNegocioUsuario.Propietario or TipoVinculacionNegocioUsuario.Gerente)).ToList();
        List<NegocioUsuarioVinculacion> principal = eligible.Where(x => x.EsPrincipal).ToList();
        NegocioUsuarioVinculacion? workerLink = eligible.Count == 1 ? eligible[0] : principal.Count == 1 ? principal[0] : null;
        if (workerLink?.Negocio is not Negocio negocio)
            return ServiceResult<PointsGroupAccrualResponse>.Failure(eligible.Count == 0 ? "forbidden" : "conflict",
                eligible.Count == 0 ? "El trabajador no tiene permisos para sumar puntos." : "El trabajador tiene varios negocios activos sin uno principal seleccionado.");
        if (negocio.RatioConversionEurosAPuntos is not > 0)
            return ServiceResult<PointsGroupAccrualResponse>.Failure("validation_error", "El negocio no tiene configurado un ratio de conversión válido.");

        request.AmountEuros = decimal.Round(request.AmountEuros, 2, MidpointRounding.AwayFromZero);
        int totalPoints = CalculatePoints(request.AmountEuros, negocio.RatioConversionEurosAPuntos.Value);
        if (totalPoints <= 0 || totalPoints < request.RecipientUserIds.Count)
            return ServiceResult<PointsGroupAccrualResponse>.Failure("validation_error", "El importe no genera puntos suficientes para repartir entre todos los clientes.");

        await using IAsyncDisposable visitLock = await _recurrence.LockAsync(negocio.Id,
            request.RecipientUserIds, cancellationToken);
        foreach (Guid userId in request.RecipientUserIds)
        {
            if (await _userCodeDirectoryService.GetUserCodeAsync(userId, cancellationToken) is null)
                return ServiceResult<PointsGroupAccrualResponse>.Failure("not_found", "Uno de los códigos QR no corresponde a un cliente válido.");
            ServiceResult<CustomerPointsLinkResult> linkResult = await EnsureCustomerLinkForPointsAsync(
                negocio.Id, userId, request.WorkerUserId, "points_worker_group_accrual", cancellationToken);
            if (!linkResult.Succeeded)
                return ServiceResult<PointsGroupAccrualResponse>.Failure(linkResult.ErrorCode ?? "validation_error", linkResult.ErrorMessage ?? "No se pudo vincular un cliente.");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        previous = await _dbContext.PointsGroupAccruals.Include(x => x.Recipients)
            .SingleOrDefaultAsync(x => x.IdempotencyKey == request.IdempotencyKey, cancellationToken);
        if (previous is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return MatchesGroupRequest(previous, request)
                ? ServiceResult<PointsGroupAccrualResponse>.Success(ToGroupResponse(previous))
                : ServiceResult<PointsGroupAccrualResponse>.Failure("conflict", "El identificador de operación ya se utilizó con otros datos.");
        }

        now = DateTime.UtcNow;
        Guid groupId = Guid.NewGuid();
        string reference = $"worker-group:{groupId:N}";
        IReadOnlyList<int> distribution = PointsGroupAccrualDistribution.Split(totalPoints, request.RecipientUserIds.Count);
        PointsGroupAccrual group = new()
        {
            Id = groupId, IdempotencyKey = request.IdempotencyKey, NegocioId = negocio.Id,
            WorkerUserId = request.WorkerUserId, AmountEuros = decimal.Round(request.AmountEuros, 2, MidpointRounding.AwayFromZero),
            TotalPoints = totalPoints, RecipientCount = request.RecipientUserIds.Count, CreatedAtUtc = now
        };
        _dbContext.PointsGroupAccruals.Add(group);
        List<(Guid UserId, int Points, int Before, int After, Guid TransactionId)> receipts = [];
        for (int i = 0; i < request.RecipientUserIds.Count; i++)
        {
            Guid recipientId = request.RecipientUserIds[i];
            Points points = await _dbContext.Points.SingleOrDefaultAsync(x => x.UserId == recipientId && x.NegocioId == negocio.Id, cancellationToken)
                ?? await GetOrCreateAsync(recipientId, negocio.Id, cancellationToken);
            Guid txId = Guid.NewGuid();
            RecurrenceResolution recurrence = await _recurrence.ResolveAsync(negocio.Id, recipientId,
                now, txId, cancellationToken);
            int amount = checked((int)decimal.Ceiling(distribution[i] * recurrence.Multiplier));
            int before = points.CurrentBalance;
            int after = checked(before + amount);
            points.CurrentBalance = after; points.TotalEarned = checked(points.TotalEarned + amount);
            points.LastEarnedAtUtc = now; points.LastMovementAtUtc = now;
            points.LastReason = Normalize(request.Reason) ?? "Compra de grupo escaneada por trabajador";
            points.LastReference = reference; points.UpdatedAtUtc = now;
            string snapshot = await _userCodeDirectoryService.GetUserCodeAsync(recipientId, cancellationToken) ?? string.Empty;
            PointsTransaction receiptTransaction = new()
            {
                Id = txId, UserId = recipientId, NegocioId = negocio.Id, PointsId = points.Id,
                ValidatorUserId = request.WorkerUserId, TransactionType = PointsTransactionType.BackofficeEarn,
                AmountEuros = i == 0 ? group.AmountEuros : null, PointsAmount = amount, BalanceBefore = before, BalanceAfter = after,
                UserCodeSnapshot = snapshot, Reason = points.LastReason, Reference = reference, CreatedAtUtc = now
            };
            RecurrenceEvaluationService.Stamp(receiptTransaction, negocio.RatioConversionEurosAPuntos.Value,
                distribution[i], recurrence);
            _dbContext.PointsTransactions.Add(receiptTransaction);
            group.Recipients.Add(new PointsGroupAccrualRecipient { Id = Guid.NewGuid(), GroupAccrualId = groupId, UserId = recipientId, ScanOrder = i, PointsAssigned = amount, TransactionId = txId });
            receipts.Add((recipientId, amount, before, after, txId));
        }
        group.TotalPoints = receipts.Sum(x => x.Points);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();
            previous = await _dbContext.PointsGroupAccruals.AsNoTracking().Include(x => x.Recipients)
                .SingleOrDefaultAsync(x => x.IdempotencyKey == request.IdempotencyKey, cancellationToken);
            if (previous is not null)
                return MatchesGroupRequest(previous, request)
                    ? ServiceResult<PointsGroupAccrualResponse>.Success(ToGroupResponse(previous))
                    : ServiceResult<PointsGroupAccrualResponse>.Failure("conflict", "El identificador de operación ya se utilizó con otros datos.");
            return ServiceResult<PointsGroupAccrualResponse>.Failure("conflict", "La operación ha cambiado concurrentemente. Reintenta con el mismo identificador.");
        }
        foreach (var receipt in receipts)
            await PublishPointsReceivedAsync(receipt.UserId, negocio.Id, receipt.Points, receipt.Before, receipt.After,
                PointsTransactionType.BackofficeEarn, "Compra de grupo escaneada por trabajador", reference, receipt.TransactionId,
                null, request.WorkerUserId, null, now, cancellationToken);
        return ServiceResult<PointsGroupAccrualResponse>.Success(ToGroupResponse(group));
    }

    public async Task<ServiceResult<PointsGroupAccrualResponse>> PreviewBackofficeGroupAccrualAsync(
        Guid authenticatedUserId, bool isAdmin, WorkerPointsGroupAccrualRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.WorkerUserId == Guid.Empty || request.AmountEuros <= 0 || request.AmountEuros > 100000m ||
            !PointsGroupAccrualDistribution.HasUniqueRecipients(request.RecipientUserIds))
            return ServiceResult<PointsGroupAccrualResponse>.Failure("validation_error", "Importe y clientes QR únicos válidos son obligatorios.");
        if (!isAdmin && authenticatedUserId != request.WorkerUserId)
            return ServiceResult<PointsGroupAccrualResponse>.Failure("forbidden", "El trabajador indicado no coincide con el usuario autenticado.");
        foreach (Guid userId in request.RecipientUserIds)
            if (await _userCodeDirectoryService.GetUserCodeAsync(userId, cancellationToken) is null)
                return ServiceResult<PointsGroupAccrualResponse>.Failure("not_found", "Uno de los códigos QR no corresponde a un cliente válido.");
        var links = await _negocioUsuarioVinculacionRepository.GetActiveByUserIdAsync(request.WorkerUserId, cancellationToken);
        DateTime now = DateTime.UtcNow;
        var eligible = links.Where(link => !link.RevokedAtUtc.HasValue &&
            (!link.FechaInicioUtc.HasValue || link.FechaInicioUtc.Value <= now) &&
            (!link.FechaFinUtc.HasValue || link.FechaFinUtc.Value >= now) && link.Negocio is { Activo: true, IsDeleted: false } &&
            (link.PuedeGestionarNegocio || link.PuedeGestionarPuntos || link.PuedeValidarTickets ||
             link.TipoVinculacion is TipoVinculacionNegocioUsuario.Propietario or TipoVinculacionNegocioUsuario.Gerente)).ToList();
        var main = eligible.Where(x => x.EsPrincipal).ToList();
        var selected = eligible.Count == 1 ? eligible[0] : main.Count == 1 ? main[0] : null;
        if (selected?.Negocio is not Negocio negocio)
            return ServiceResult<PointsGroupAccrualResponse>.Failure("forbidden", "No se ha podido determinar el negocio activo del trabajador.");
        if (negocio.RatioConversionEurosAPuntos is not > 0)
            return ServiceResult<PointsGroupAccrualResponse>.Failure("validation_error", "El negocio no tiene configurado un ratio de conversión válido.");
        request.AmountEuros = decimal.Round(request.AmountEuros, 2, MidpointRounding.AwayFromZero);
        int total = CalculatePoints(request.AmountEuros, negocio.RatioConversionEurosAPuntos.Value);
        if (total < request.RecipientUserIds.Count)
            return ServiceResult<PointsGroupAccrualResponse>.Failure("validation_error", "El importe no genera puntos suficientes para repartir entre todos los clientes.");
        IReadOnlyList<int> distribution = PointsGroupAccrualDistribution.Split(total, request.RecipientUserIds.Count);
        List<PointsGroupAccrualRecipientResponse> previewRecipients = [];
        for (int index = 0; index < request.RecipientUserIds.Count; index++)
        {
            Guid recipientId = request.RecipientUserIds[index];
            RecurrenceResolution recurrence = await _recurrence.ResolveAsync(negocio.Id, recipientId,
                now, Guid.NewGuid(), cancellationToken);
            previewRecipients.Add(new PointsGroupAccrualRecipientResponse
            {
                UserId = recipientId,
                PointsAssigned = checked((int)decimal.Ceiling(distribution[index] * recurrence.Multiplier))
            });
        }
        return ServiceResult<PointsGroupAccrualResponse>.Success(new PointsGroupAccrualResponse
        {
            NegocioId = negocio.Id, WorkerUserId = request.WorkerUserId, AmountEuros = decimal.Round(request.AmountEuros, 2),
            TotalPoints = previewRecipients.Sum(x => x.PointsAssigned), RecipientCount = request.RecipientUserIds.Count,
            Recipients = previewRecipients
        });
    }

    private static PointsGroupAccrualResponse ToGroupResponse(PointsGroupAccrual group) => new()
    {
        Id = group.Id, IdempotencyKey = group.IdempotencyKey, NegocioId = group.NegocioId, WorkerUserId = group.WorkerUserId,
        AmountEuros = group.AmountEuros, TotalPoints = group.TotalPoints, RecipientCount = group.RecipientCount, CreatedAtUtc = group.CreatedAtUtc,
        Recipients = group.Recipients.OrderBy(x => x.ScanOrder).Select(x => new PointsGroupAccrualRecipientResponse { UserId = x.UserId, PointsAssigned = x.PointsAssigned }).ToList()
    };

    private static bool MatchesGroupRequest(PointsGroupAccrual group, WorkerPointsGroupAccrualRequest request)
        => group.WorkerUserId == request.WorkerUserId &&
           group.AmountEuros == decimal.Round(request.AmountEuros, 2, MidpointRounding.AwayFromZero) &&
           group.RecipientCount == request.RecipientUserIds.Count &&
           group.Recipients.OrderBy(x => x.ScanOrder).Select(x => x.UserId).SequenceEqual(request.RecipientUserIds);

    public async Task<ServiceResult<IReadOnlyCollection<PointsFailedAttemptResponse>>> GetFailedAttemptsAsync(
        Guid negocioId,
        Guid requesterUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        ServiceResult authorization = await EnsureCanManagePointsAsync(negocioId, requesterUserId, isAdmin, cancellationToken);
        if (!authorization.Succeeded)
        {
            return ServiceResult<IReadOnlyCollection<PointsFailedAttemptResponse>>.Failure(
                authorization.ErrorCode ?? "forbidden",
                authorization.ErrorMessage ?? "Sin permisos.");
        }

        IReadOnlyCollection<PointsFailedAttemptResponse> attempts =
            (await _pointsOperationAttemptRepository.GetByNegocioAsync(negocioId, cancellationToken))
            .Where(attempt => !attempt.Succeeded)
            .Select(attempt => attempt.ToResponse())
            .ToArray();

        return ServiceResult<IReadOnlyCollection<PointsFailedAttemptResponse>>.Success(attempts);
    }

    public async Task<ServiceResult<GiftPointsResponse>> GiftPointsAsync(
        Guid senderUserId,
        Guid negocioId,
        GiftPointsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0)
        {
            return ServiceResult<GiftPointsResponse>.Failure("validation_error", "La cantidad de puntos a regalar debe ser mayor que cero.");
        }

        Negocio? negocio = await _negocioRepository.GetByIdAsync(negocioId, cancellationToken);
        if (negocio is null || !negocio.Activo || negocio.IsDeleted)
        {
            return ServiceResult<GiftPointsResponse>.Failure("not_found", "El negocio no existe o no est\u00e1 activo.");
        }

        Guid? recipientUserId = await ResolveRecipientUserIdAsync(request, cancellationToken);
        if (!recipientUserId.HasValue)
        {
            return ServiceResult<GiftPointsResponse>.Failure("not_found", "No se ha encontrado el usuario destinatario.");
        }

        if (recipientUserId.Value == senderUserId)
        {
            return ServiceResult<GiftPointsResponse>.Failure("validation_error", "No puedes regalarte puntos a ti mismo.");
        }

        NegocioAudiencia? senderAudience = await GetActiveAudienceAsync(negocioId, senderUserId, cancellationToken);
        if (senderAudience is null)
        {
            return ServiceResult<GiftPointsResponse>.Failure("forbidden", "El usuario emisor no forma parte de la audiencia activa del negocio.");
        }

        NegocioAudiencia? existingRecipientAudience =
            await _negociosDbContext.NegociosAudiencias.FirstOrDefaultAsync(
                audience => audience.NegocioId == negocioId && audience.UserId == recipientUserId.Value,
                cancellationToken);

        bool recipientWasFirstLink = existingRecipientAudience is null;
        bool recipientWasLinked = IsAudienceActive(existingRecipientAudience);

        if (!recipientWasLinked)
        {
            ServiceResult<NegocioAudiencia> linkResult = await _negocioAudienciaService.EnsureAudienceAsync(
                negocioId,
                recipientUserId.Value,
                "points_gift",
                cancellationToken);

            if (!linkResult.Succeeded)
            {
                return ServiceResult<GiftPointsResponse>.Failure(
                    linkResult.ErrorCode ?? "validation_error",
                    linkResult.ErrorMessage ?? "No se ha podido vincular al usuario destinatario con el negocio.");
            }
        }

        string senderUserCode = await _userCodeDirectoryService.EnsureUserCodeAsync(senderUserId, cancellationToken);
        string recipientUserCode = await _userCodeDirectoryService.EnsureUserCodeAsync(recipientUserId.Value, cancellationToken);

        Points senderPoints = await GetOrCreateAsync(senderUserId, negocioId, cancellationToken);
        Points recipientPoints = await GetOrCreateAsync(recipientUserId.Value, negocioId, cancellationToken);

        if (senderPoints.CurrentBalance < request.Amount)
        {
            return ServiceResult<GiftPointsResponse>.Failure("insufficient_balance", "El usuario no tiene suficientes puntos para regalar.");
        }

        DateTime now = DateTime.UtcNow;
        string outgoingReason = Normalize(request.Reason) ?? $"Regalo de {request.Amount} puntos a otro cliente";
        string incomingReason = Normalize(request.Reason) ?? $"Puntos recibidos de {senderUserCode}";
        string reference = Normalize(request.Reference) ?? $"gift:{senderUserId:N}:{recipientUserId.Value:N}";

        await ApplyDebitAsync(
            senderPoints,
            request.Amount,
            now,
            outgoingReason,
            reference,
            PointsTransactionType.TransferOut,
            senderUserCode,
            recipientUserId.Value,
            recipientUserCode,
            cancellationToken);
        await _negocioAudienciaService.TouchAudienceActivityAsync(
            negocioId,
            senderUserId,
            "points_gift_sent",
            cancellationToken);

        await ApplyCreditAsync(
            recipientPoints,
            request.Amount,
            now,
            incomingReason,
            reference,
            PointsTransactionType.TransferIn,
            recipientUserCode,
            senderUserId,
            senderUserCode,
            cancellationToken);
        await _negocioAudienciaService.TouchAudienceActivityAsync(
            negocioId,
            recipientUserId.Value,
            "points_gift_received",
            cancellationToken);

        bool recipientReceivedWelcomeTicket = false;
        bool senderReceivedReferralTicket = false;

        if (recipientWasFirstLink)
        {
            recipientReceivedWelcomeTicket =
                await _registrationRewardService.AssignBusinessWelcomeTicketAsync(negocioId, recipientUserId.Value, cancellationToken);

            if (negocio.PermiteProgramaReferidos)
            {
                senderReceivedReferralTicket =
                    await _registrationRewardService.AssignBusinessReferralTicketAsync(negocioId, senderUserId, cancellationToken);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await PublishPointsReceivedAsync(
            recipientUserId.Value,
            negocioId,
            request.Amount,
            recipientPoints.CurrentBalance - request.Amount,
            recipientPoints.CurrentBalance,
            PointsTransactionType.TransferIn,
            incomingReason,
            reference,
            transactionId: null,
            operationId: null,
            validatorUserId: null,
            counterpartyUserId: senderUserId,
            createdAtUtc: now,
            cancellationToken: cancellationToken);

        return ServiceResult<GiftPointsResponse>.Success(new GiftPointsResponse
        {
            NegocioId = negocioId,
            SenderUserId = senderUserId,
            RecipientUserId = recipientUserId.Value,
            PointsTransferred = request.Amount,
            SenderBalanceAfter = senderPoints.CurrentBalance,
            RecipientBalanceAfter = recipientPoints.CurrentBalance,
            RecipientWasLinked = recipientWasLinked,
            RecipientReceivedWelcomeTicket = recipientReceivedWelcomeTicket,
            SenderReceivedReferralTicket = senderReceivedReferralTicket,
            RecipientUserCode = recipientUserCode,
            Message = recipientWasFirstLink
                ? "Los puntos se han regalado y el destinatario ha quedado vinculado al negocio por primera vez."
                : "Los puntos se han regalado correctamente."
        });
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

        ServiceResult<CustomerPointsLinkResult> linkResult = await EnsureCustomerLinkForPointsAsync(
            negocioId,
            userId,
            null,
            "points_direct_add",
            cancellationToken);
        if (!linkResult.Succeeded)
        {
            return ServiceResult<PointsSummary>.Failure(
                linkResult.ErrorCode ?? "validation_error",
                linkResult.ErrorMessage ?? "No se ha podido vincular al usuario con el negocio.");
        }

        Points points = await GetOrCreateAsync(userId, negocioId, cancellationToken);
        DateTime now = DateTime.UtcNow;

        string userCode = await _userCodeDirectoryService.EnsureUserCodeAsync(userId, cancellationToken);

        await ApplyCreditAsync(
            points,
            amount,
            now,
            Normalize(reason),
            Normalize(reference),
            PointsTransactionType.Earn,
            userCode,
            null,
            null,
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await PublishPointsReceivedAsync(
            userId,
            negocioId,
            amount,
            points.CurrentBalance - amount,
            points.CurrentBalance,
            PointsTransactionType.Earn,
            Normalize(reason),
            Normalize(reference),
            transactionId: null,
            operationId: null,
            validatorUserId: null,
            counterpartyUserId: null,
            createdAtUtc: now,
            cancellationToken: cancellationToken);
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
        string userCode = await _userCodeDirectoryService.EnsureUserCodeAsync(userId, cancellationToken);

        await ApplyDebitAsync(
            points,
            amount,
            now,
            Normalize(reason),
            Normalize(reference),
            PointsTransactionType.Spend,
            userCode,
            null,
            null,
            cancellationToken);
        await _negocioAudienciaService.TouchAudienceActivityAsync(
            negocioId,
            userId,
            "points_spend",
            cancellationToken);

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

    private async Task<ServiceResult<CustomerPointsLinkResult>> EnsureCustomerLinkForPointsAsync(
        Guid negocioId,
        Guid userId,
        Guid? linkedByUserId,
        string origin,
        CancellationToken cancellationToken)
    {
        NegocioAudiencia? existingAudience =
            await _negociosDbContext.NegociosAudiencias.FirstOrDefaultAsync(
                audience => audience.NegocioId == negocioId && audience.UserId == userId,
                cancellationToken);

        bool wasFirstLink = existingAudience is null;
        if (IsAudienceActive(existingAudience))
        {
            await _negocioAudienciaService.TouchAudienceActivityAsync(negocioId, userId, origin, cancellationToken);
            return ServiceResult<CustomerPointsLinkResult>.Success(new CustomerPointsLinkResult(
                WasFirstLink: false,
                WasLinkedNow: false,
                ReceivedWelcomeTicket: false));
        }

        ServiceResult<NegocioAudiencia> linkResult = await _negocioAudienciaService.EnsureAudienceAsync(
            negocioId,
            userId,
            origin,
            cancellationToken);

        if (!linkResult.Succeeded)
        {
            return ServiceResult<CustomerPointsLinkResult>.Failure(
                linkResult.ErrorCode ?? "validation_error",
                linkResult.ErrorMessage ?? "No se ha podido vincular al usuario con el negocio.");
        }

        bool receivedWelcomeTicket = wasFirstLink &&
            await _registrationRewardService.AssignBusinessWelcomeTicketAsync(negocioId, userId, cancellationToken);

        return ServiceResult<CustomerPointsLinkResult>.Success(new CustomerPointsLinkResult(
            WasFirstLink: wasFirstLink,
            WasLinkedNow: true,
            ReceivedWelcomeTicket: receivedWelcomeTicket));
    }

    private async Task<Guid?> ResolveRecipientUserIdAsync(GiftPointsRequest request, CancellationToken cancellationToken)
    {
        Guid? resolvedFromCode = null;

        if (!string.IsNullOrWhiteSpace(request.RecipientUserCode))
        {
            resolvedFromCode = await _userCodeDirectoryService.ResolveUserIdAsync(request.RecipientUserCode.Trim(), cancellationToken);
        }

        if (request.RecipientUserId.HasValue && resolvedFromCode.HasValue && request.RecipientUserId.Value != resolvedFromCode.Value)
        {
            return null;
        }

        return request.RecipientUserId ?? resolvedFromCode;
    }

    private async Task<NegocioUsuarioVinculacion?> GetActiveLinkAsync(Guid negocioId, Guid userId, CancellationToken cancellationToken)
    {
        NegocioUsuarioVinculacion? link =
            await _negocioUsuarioVinculacionRepository.GetByNegocioAndUserAsync(negocioId, userId, cancellationToken);

        return IsLinkActive(link) ? link : null;
    }

    private async Task<NegocioAudiencia?> GetActiveAudienceAsync(Guid negocioId, Guid userId, CancellationToken cancellationToken)
    {
        NegocioAudiencia? audience = await _negociosDbContext.NegociosAudiencias
            .FirstOrDefaultAsync(item => item.NegocioId == negocioId && item.UserId == userId, cancellationToken);

        return IsAudienceActive(audience) ? audience : null;
    }

    private static bool IsLinkActive(NegocioUsuarioVinculacion? link)
    {
        if (link is null || !link.Activa || link.RevokedAtUtc.HasValue)
        {
            return false;
        }

        DateTime now = DateTime.UtcNow;
        return (!link.FechaInicioUtc.HasValue || link.FechaInicioUtc.Value <= now) &&
               (!link.FechaFinUtc.HasValue || link.FechaFinUtc.Value >= now);
    }

    private static bool IsAudienceActive(NegocioAudiencia? audience)
        => audience is not null && audience.Activa && !audience.FechaBajaUtc.HasValue;

    private async Task ApplyCreditAsync(
        Points points,
        int amount,
        DateTime now,
        string? reason,
        string? reference,
        PointsTransactionType transactionType,
        string userCodeSnapshot,
        Guid? counterpartyUserId,
        string? counterpartyUserCodeSnapshot,
        CancellationToken cancellationToken)
    {
        int balanceBefore = points.CurrentBalance;
        int balanceAfter = balanceBefore + amount;

        points.CurrentBalance = balanceAfter;
        points.TotalEarned += amount;
        points.LastEarnedAtUtc = now;
        points.LastMovementAtUtc = now;
        points.LastReason = Normalize(reason);
        points.LastReference = Normalize(reference);
        points.UpdatedAtUtc = now;

        await _pointsTransactionRepository.AddAsync(
            new PointsTransaction
            {
                Id = Guid.NewGuid(),
                UserId = points.UserId,
                NegocioId = points.NegocioId,
                PointsId = points.Id,
                CounterpartyUserId = counterpartyUserId,
                TransactionType = transactionType,
                PointsAmount = amount,
                BalanceBefore = balanceBefore,
                BalanceAfter = balanceAfter,
                UserCodeSnapshot = userCodeSnapshot,
                CounterpartyUserCodeSnapshot = counterpartyUserCodeSnapshot,
                Reason = Normalize(reason),
                Reference = Normalize(reference),
                CreatedAtUtc = now
            },
            cancellationToken);
    }

    private async Task ApplyDebitAsync(
        Points points,
        int amount,
        DateTime now,
        string? reason,
        string? reference,
        PointsTransactionType transactionType,
        string userCodeSnapshot,
        Guid? counterpartyUserId,
        string? counterpartyUserCodeSnapshot,
        CancellationToken cancellationToken)
    {
        int balanceBefore = points.CurrentBalance;
        int balanceAfter = balanceBefore - amount;

        points.CurrentBalance = balanceAfter;
        points.TotalSpent += amount;
        points.LastSpentAtUtc = now;
        points.LastMovementAtUtc = now;
        points.LastReason = Normalize(reason);
        points.LastReference = Normalize(reference);
        points.UpdatedAtUtc = now;

        await _pointsTransactionRepository.AddAsync(
            new PointsTransaction
            {
                Id = Guid.NewGuid(),
                UserId = points.UserId,
                NegocioId = points.NegocioId,
                PointsId = points.Id,
                CounterpartyUserId = counterpartyUserId,
                TransactionType = transactionType,
                PointsAmount = amount,
                BalanceBefore = balanceBefore,
                BalanceAfter = balanceAfter,
                UserCodeSnapshot = userCodeSnapshot,
                CounterpartyUserCodeSnapshot = counterpartyUserCodeSnapshot,
                Reason = Normalize(reason),
                Reference = Normalize(reference),
                CreatedAtUtc = now
            },
            cancellationToken);
    }

    private Task PublishPointsReceivedAsync(PointsTransaction transaction, CancellationToken cancellationToken)
        => PublishPointsReceivedAsync(
            transaction.UserId,
            transaction.NegocioId,
            transaction.PointsAmount,
            transaction.BalanceBefore,
            transaction.BalanceAfter,
            transaction.TransactionType,
            transaction.Reason,
            transaction.Reference,
            transaction.Id,
            transaction.OperationId,
            transaction.ValidatorUserId,
            transaction.CounterpartyUserId,
            transaction.CreatedAtUtc,
            cancellationToken);

    private Task PublishPointsReceivedAsync(
        Guid userId,
        Guid negocioId,
        int pointsAmount,
        int balanceBefore,
        int balanceAfter,
        PointsTransactionType transactionType,
        string? reason,
        string? reference,
        Guid? transactionId,
        Guid? operationId,
        Guid? validatorUserId,
        Guid? counterpartyUserId,
        DateTime createdAtUtc,
        CancellationToken cancellationToken)
        => _userEventPublisher.PublishAsync(
            userId,
            new UserAppEvent
            {
                Type = "fidelity.points.received",
                OccurredAtUtc = createdAtUtc,
                Payload = new PointsReceivedEventPayload
                {
                    UserId = userId,
                    NegocioId = negocioId,
                    TransactionId = transactionId,
                    OperationId = operationId,
                    ValidatorUserId = validatorUserId,
                    CounterpartyUserId = counterpartyUserId,
                    PointsAmount = pointsAmount,
                    BalanceBefore = balanceBefore,
                    BalanceAfter = balanceAfter,
                    TransactionType = transactionType.ToString(),
                    Reason = reason,
                    Reference = reference,
                    CreatedAtUtc = createdAtUtc
                }
            },
            cancellationToken);

    private async Task<ServiceResult> EnsureCanManagePointsAsync(Guid negocioId, Guid requesterUserId, bool isAdmin, CancellationToken cancellationToken)
    {
        if (isAdmin)
        {
            return ServiceResult.Success();
        }

        NegocioUsuarioVinculacion? link =
            await _negocioUsuarioVinculacionRepository.GetByNegocioAndUserAsync(negocioId, requesterUserId, cancellationToken);

        DateTime now = DateTime.UtcNow;
        if (link is null || !link.Activa || link.RevokedAtUtc.HasValue ||
            link.FechaInicioUtc.HasValue && link.FechaInicioUtc.Value > now ||
            link.FechaFinUtc.HasValue && link.FechaFinUtc.Value < now)
        {
            return ServiceResult.Failure("forbidden", "El usuario no está vinculado al negocio.");
        }

        if (!link.PuedeGestionarNegocio && !link.PuedeGestionarPuntos && !link.PuedeValidarTickets)
        {
            return ServiceResult.Failure("forbidden", "El usuario vinculado al negocio no tiene permisos para gestionar puntos.");
        }

        return ServiceResult.Success();
    }

    private static int CalculatePoints(decimal amountEuros, decimal ratio)
        => (int)decimal.Ceiling(amountEuros * ratio);

    private async Task<ServiceResult<PointsEarnValidationResponse>?> CheckDirectReplayAsync(
        Guid negocioId, Guid userId, decimal amountEuros, Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (idempotencyKey == Guid.Empty)
            return await _recurrence.HasActiveRulesAsync(negocioId, cancellationToken)
                ? ServiceResult<PointsEarnValidationResponse>.Failure("validation_error",
                    "Esta acumulación requiere un identificador de operación para evitar visitas duplicadas.")
                : null;
        PointsTransaction? previous = await _recurrence.FindReplayAsync(negocioId,
            idempotencyKey, cancellationToken);
        if (previous is null) return null;
        if (previous.UserId != userId || previous.AmountEuros != decimal.Round(amountEuros, 2,
                MidpointRounding.AwayFromZero) || previous.TransactionType != PointsTransactionType.BackofficeEarn)
            return ServiceResult<PointsEarnValidationResponse>.Failure("conflict",
                "El identificador de operación ya se usó con otros datos.");
        return ServiceResult<PointsEarnValidationResponse>.Success(new PointsEarnValidationResponse
        {
            OperationId = idempotencyKey,
            UserId = userId,
            NegocioId = negocioId,
            PointsEarned = previous.PointsAmount,
            TotalBalance = previous.BalanceAfter,
            ValidatorUserId = previous.ValidatorUserId ?? Guid.Empty,
            Message = "Operación ya acreditada; se devuelve el resultado original."
        });
    }

    private static bool VerifyMasterPin(string masterPin, string storedHash)
    {
        byte[] computedHash = SHA256.HashData(Encoding.UTF8.GetBytes(masterPin.Trim()));
        return string.Equals(Convert.ToHexString(computedHash), storedHash, StringComparison.OrdinalIgnoreCase);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record CustomerPointsLinkResult(
        bool WasFirstLink,
        bool WasLinkedNow,
        bool ReceivedWelcomeTicket);
}
