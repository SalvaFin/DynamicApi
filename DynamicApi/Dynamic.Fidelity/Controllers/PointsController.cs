using System.Security.Claims;
using Dynamic.Fidelity.Application.Common;
using Dynamic.Fidelity.Application.Contracts.Services;
using Dynamic.Fidelity.Application.DTOs.Requests;
using Dynamic.Fidelity.Application.DTOs.Responses;
using Dynamic.Fidelity.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dynamic.Fidelity.Controllers;

[ApiController]
[Authorize]
[Route("api/fidelity/negocios/{negocioId:guid}/points")]
public class PointsController : ControllerBase
{
    private readonly IPointsService _pointsService;

    public PointsController(IPointsService pointsService)
    {
        _pointsService = pointsService;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyBalance(Guid negocioId, CancellationToken cancellationToken)
    {
        Guid? userId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<PointsSummary> result = await _pointsService.GetBalanceAsync(userId.Value, negocioId, cancellationToken);
        return ToActionResult(result, Ok);
    }

    [HttpGet("me/transactions")]
    public async Task<IActionResult> GetMyTransactions(Guid negocioId, CancellationToken cancellationToken)
    {
        Guid? userId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<IReadOnlyCollection<PointsTransactionResponse>> result =
            await _pointsService.GetTransactionsAsync(userId.Value, negocioId, cancellationToken);

        return ToActionResult(result, Ok);
    }

    [HttpPost("gifts")]
    public async Task<IActionResult> GiftPoints(
        Guid negocioId,
        [FromBody] GiftPointsRequest request,
        CancellationToken cancellationToken)
    {
        Guid? senderUserId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!senderUserId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<GiftPointsResponse> result =
            await _pointsService.GiftPointsAsync(senderUserId.Value, negocioId, request, cancellationToken);

        return ToActionResult(result, data => StatusCode(StatusCodes.Status201Created, data));
    }

    [HttpPost("earn-operations")]
    public async Task<IActionResult> InitiateEarnOperation(
        Guid negocioId,
        [FromBody] InitiatePointsEarnRequest request,
        CancellationToken cancellationToken)
    {
        Guid? userId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<PointsEarnOperationResponse> result =
            await _pointsService.InitiateEarnOperationAsync(userId.Value, negocioId, request, cancellationToken);

        return ToActionResult(result, data => StatusCode(StatusCodes.Status201Created, data));
    }

    [Authorize(Policy = "BusinessStaffAuth")]
    [HttpPost("earn-operations/{operationId:guid}/validate")]
    public async Task<IActionResult> ValidateEarnOperation(
        Guid negocioId,
        Guid operationId,
        [FromBody] ValidatePointsEarnOperationRequest request,
        CancellationToken cancellationToken)
    {
        Guid? validatorUserId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!validatorUserId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<PointsEarnValidationResponse> result =
            await _pointsService.ValidateEarnOperationAsync(operationId, validatorUserId.Value, User.IsInRole("Admin"), request, cancellationToken);

        return ToActionResult(result, Ok);
    }

    [Authorize(Policy = "BusinessStaffAuth")]
    [HttpPost("backoffice/accrual")]
    public async Task<IActionResult> BackofficeAccrual(
        Guid negocioId,
        [FromBody] BackofficeAccrualByUserCodeRequest request,
        CancellationToken cancellationToken)
    {
        Guid? validatorUserId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!validatorUserId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<PointsEarnValidationResponse> result =
            await _pointsService.BackofficeAccrualByUserCodeAsync(
                negocioId,
                validatorUserId.Value,
                User.IsInRole("Admin"),
                request,
                cancellationToken);

        return ToActionResult(result, Ok);
    }

    [Authorize(Policy = "BusinessStaffAuth")]
    [HttpPost("/api/fidelity/points/backoffice/worker-accrual")]
    public async Task<IActionResult> BackofficeWorkerAccrual(
        [FromBody] WorkerPointsAccrualRequest request,
        CancellationToken cancellationToken)
    {
        Guid? authenticatedUserId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!authenticatedUserId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<PointsEarnValidationResponse> result =
            await _pointsService.BackofficeAccrualByWorkerAsync(
                authenticatedUserId.Value,
                User.IsInRole("Admin"),
                request,
                cancellationToken);

        return ToActionResult(result, Ok);
    }

    [Authorize(Policy = "BusinessStaffAuth")]
    [HttpGet("users/{userId:guid}/transactions")]
    public async Task<IActionResult> GetUserTransactions(Guid negocioId, Guid userId, CancellationToken cancellationToken)
    {
        ServiceResult<IReadOnlyCollection<PointsTransactionResponse>> result =
            await _pointsService.GetTransactionsAsync(userId, negocioId, cancellationToken);

        return ToActionResult(result, Ok);
    }

    [Authorize(Policy = "BusinessStaffAuth")]
    [HttpGet("failed-attempts")]
    public async Task<IActionResult> GetFailedAttempts(Guid negocioId, CancellationToken cancellationToken)
    {
        Guid? requesterUserId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!requesterUserId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<IReadOnlyCollection<PointsFailedAttemptResponse>> result =
            await _pointsService.GetFailedAttemptsAsync(negocioId, requesterUserId.Value, User.IsInRole("Admin"), cancellationToken);

        return ToActionResult(result, Ok);
    }

    private IActionResult ToActionResult(ServiceResult result, Func<IActionResult> onSuccess)
    {
        if (result.Succeeded)
        {
            return onSuccess();
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    private IActionResult ToActionResult<T>(ServiceResult<T> result, Func<T, IActionResult> onSuccess)
    {
        if (result.Succeeded && result.Data is not null)
        {
            return onSuccess(result.Data);
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    private IActionResult MapFailure(string? errorCode, string? errorMessage)
        => errorCode switch
        {
            "validation_error" => BadRequest(new { message = errorMessage }),
            "not_found" => NotFound(new { message = errorMessage }),
            "insufficient_balance" => BadRequest(new { message = errorMessage }),
            "conflict" => Conflict(new { message = errorMessage }),
            "forbidden" => StatusCode(StatusCodes.Status403Forbidden, new { message = errorMessage }),
            "locked" => StatusCode(StatusCodes.Status423Locked, new { message = errorMessage }),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new { message = errorMessage ?? "Error interno del servidor." })
        };

    private Guid? GetClaimGuid(params string[] claimTypes)
    {
        foreach (string claimType in claimTypes)
        {
            string? value = User.FindFirstValue(claimType);
            if (Guid.TryParse(value, out Guid parsedValue))
            {
                return parsedValue;
            }
        }

        return null;
    }
}
