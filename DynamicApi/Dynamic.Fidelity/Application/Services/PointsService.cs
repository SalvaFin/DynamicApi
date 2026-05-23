using System.Security.Cryptography;
using System.Text;
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
using Dynamic.Negocios.Application.Contracts.Services;
using Dynamic.Negocios.Application.DTOs.Requests;
using Dynamic.Negocios.Domain.Entities;
using Dynamic.Negocios.Domain.Enums;

namespace Dynamic.Fidelity.Application.Services;

public class PointsService : IPointsService
{
    private readonly DynamicFidelityDbContext _dbContext;
    private readonly IPointsRepository _pointsRepository;
    private readonly IPointsTransactionRepository _pointsTransactionRepository;
    private readonly IPointsOperationRepository _pointsOperationRepository;
    private readonly IPointsOperationAttemptRepository _pointsOperationAttemptRepository;
    private readonly IUserCodeDirectoryService _userCodeDirectoryService;
    private readonly INegocioRepository _negocioRepository;
    private readonly INegocioUsuarioVinculacionRepository _negocioUsuarioVinculacionRepository;
    private readonly INegocioUsuarioVinculacionService _negocioUsuarioVinculacionService;
    private readonly IRegistrationRewardService _registrationRewardService;

    public PointsService(
        DynamicFidelityDbContext dbContext,
        IPointsRepository pointsRepository,
        IPointsTransactionRepository pointsTransactionRepository,
        IPointsOperationRepository pointsOperationRepository,
        IPointsOperationAttemptRepository pointsOperationAttemptRepository,
        IUserCodeDirectoryService userCodeDirectoryService,
        INegocioRepository negocioRepository,
        INegocioUsuarioVinculacionRepository negocioUsuarioVinculacionRepository,
        INegocioUsuarioVinculacionService negocioUsuarioVinculacionService,
        IRegistrationRewardService registrationRewardService)
    {
        _dbContext = dbContext;
        _pointsRepository = pointsRepository;
        _pointsTransactionRepository = pointsTransactionRepository;
        _pointsOperationRepository = pointsOperationRepository;
        _pointsOperationAttemptRepository = pointsOperationAttemptRepository;
        _userCodeDirectoryService = userCodeDirectoryService;
        _negocioRepository = negocioRepository;
        _negocioUsuarioVinculacionRepository = negocioUsuarioVinculacionRepository;
        _negocioUsuarioVinculacionService = negocioUsuarioVinculacionService;
        _registrationRewardService = registrationRewardService;
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
            Id = Guid.NewGuid(),
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

        Points points = await GetOrCreateAsync(userId.Value, negocioId, cancellationToken);
        DateTime now = DateTime.UtcNow;
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

        await _pointsTransactionRepository.AddAsync(
            new PointsTransaction
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                NegocioId = negocioId,
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
            },
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<PointsEarnValidationResponse>.Success(new PointsEarnValidationResponse
        {
            OperationId = Guid.Empty,
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

        await _pointsTransactionRepository.AddAsync(
            new PointsTransaction
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                NegocioId = negocio.Id,
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
            },
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<PointsEarnValidationResponse>.Success(new PointsEarnValidationResponse
        {
            OperationId = Guid.Empty,
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

        NegocioUsuarioVinculacion? senderLink = await GetActiveLinkAsync(negocioId, senderUserId, cancellationToken);
        if (senderLink is null)
        {
            return ServiceResult<GiftPointsResponse>.Failure("forbidden", "El usuario emisor no est\u00e1 vinculado activamente al negocio.");
        }

        NegocioUsuarioVinculacion? existingRecipientLink =
            await _negocioUsuarioVinculacionRepository.GetByNegocioAndUserAsync(negocioId, recipientUserId.Value, cancellationToken);

        bool recipientWasFirstLink = existingRecipientLink is null;
        bool recipientWasLinked = IsLinkActive(existingRecipientLink);

        if (!recipientWasLinked)
        {
            var linkResult = await _negocioUsuarioVinculacionService.LinkUserAsync(
                    negocioId,
                    recipientUserId.Value,
                    new VincularUsuarioNegocioRequest
                    {
                        TipoVinculacion = TipoVinculacionNegocioUsuario.Cliente,
                        TituloRelacion = "Cliente",
                        EsPrincipal = false,
                        PuedeAccederBackoffice = false,
                        PuedeGestionarNegocio = false,
                        PuedeGestionarClientes = false,
                        PuedeGestionarCampanas = false,
                        PuedeGestionarPuntos = false,
                        PuedeValidarTickets = false,
                        PuedeVerReportes = false,
                        OrigenVinculacion = "points_gift"
                    },
                    linkedByUserId: senderUserId,
                    cancellationToken: cancellationToken);

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

    private async Task<ServiceResult> EnsureCanManagePointsAsync(Guid negocioId, Guid requesterUserId, bool isAdmin, CancellationToken cancellationToken)
    {
        if (isAdmin)
        {
            return ServiceResult.Success();
        }

        NegocioUsuarioVinculacion? link =
            await _negocioUsuarioVinculacionRepository.GetByNegocioAndUserAsync(negocioId, requesterUserId, cancellationToken);

        if (link is null || !link.Activa)
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

    private static bool VerifyMasterPin(string masterPin, string storedHash)
    {
        byte[] computedHash = SHA256.HashData(Encoding.UTF8.GetBytes(masterPin.Trim()));
        return string.Equals(Convert.ToHexString(computedHash), storedHash, StringComparison.OrdinalIgnoreCase);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
