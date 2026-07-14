using System.Security.Claims;
using Dynamic.Fidelity.Application.Contracts.Services;
using Dynamic.Fidelity.Application.DTOs.Requests;
using Dynamic.Users.Application.Common;
using Dynamic.Users.Application.Contracts.Services;
using Dynamic.Users.Application.DTOs.Requests;
using Dynamic.Users.Application.DTOs.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dynamic.Users.Controllers;

[ApiController]
[Route("api/users/business-accounts")]
public class BusinessUserAccountsController : ControllerBase
{
    private readonly IBusinessUserProvisioningService _businessUserProvisioningService;
    private readonly IUserService _userService;
    private readonly IPointsService _pointsService;

    public BusinessUserAccountsController(
        IBusinessUserProvisioningService businessUserProvisioningService,
        IUserService userService,
        IPointsService pointsService)
    {
        _businessUserProvisioningService = businessUserProvisioningService;
        _userService = userService;
        _pointsService = pointsService;
    }

    [Authorize(Policy = "AdminAuth")]
    [HttpGet("admin/negocios/{negocioId:guid}")]
    public async Task<IActionResult> GetBusinessAccountsByAdmin(
        Guid negocioId,
        CancellationToken cancellationToken)
    {
        ServiceResult<IReadOnlyCollection<BusinessUserAccountResponse>> result =
            await _businessUserProvisioningService.GetBusinessAccountsByAdminAsync(negocioId, cancellationToken);

        return ToActionResult(result, Ok);
    }

    [Authorize(Policy = "AdminAuth")]
    [HttpPost("admin/negocios/{negocioId:guid}/owner")]
    public async Task<IActionResult> CreateOwnerByAdmin(
        Guid negocioId,
        [FromBody] CreateBusinessManagedUserRequest request,
        CancellationToken cancellationToken)
    {
        ServiceResult<ProvisionedBusinessUserResponse> result =
            await _businessUserProvisioningService.CreateOwnerAccountByAdminAsync(negocioId, request, cancellationToken);

        return ToActionResult(result, data => StatusCode(StatusCodes.Status201Created, data));
    }

    [Authorize(Policy = "AdminAuth")]
    [HttpPost("admin/negocios/{negocioId:guid}/workers")]
    public async Task<IActionResult> CreateWorkerByAdmin(
        Guid negocioId,
        [FromBody] CreateBusinessManagedUserRequest request,
        CancellationToken cancellationToken)
    {
        ServiceResult<ProvisionedBusinessUserResponse> result =
            await _businessUserProvisioningService.CreateWorkerAccountByAdminAsync(negocioId, request, cancellationToken);

        return ToActionResult(result, data => StatusCode(StatusCodes.Status201Created, data));
    }

    [Authorize]
    [HttpPost("my-businesses/{negocioId:guid}/workers")]
    public async Task<IActionResult> CreateWorkerByOwner(
        Guid negocioId,
        [FromBody] CreateBusinessManagedUserRequest request,
        CancellationToken cancellationToken)
    {
        Guid? requesterUserId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!requesterUserId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<ProvisionedBusinessUserResponse> result =
            await _businessUserProvisioningService.CreateWorkerAccountByOwnerAsync(
                negocioId,
                requesterUserId.Value,
                User.IsInRole("Admin"),
                request,
                cancellationToken);

        return ToActionResult(result, data => StatusCode(StatusCodes.Status201Created, data));
    }

    [Authorize(Policy = "BusinessStaffAuth")]
    [HttpPost("my-businesses/{negocioId:guid}/customers")]
    public async Task<IActionResult> CreateCustomerByBusinessStaff(
        Guid negocioId,
        [FromBody] CreateBusinessCustomerUserRequest request,
        CancellationToken cancellationToken)
    {
        Guid? requesterUserId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!requesterUserId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<BusinessCustomerRegistrationResponse> result =
            await _businessUserProvisioningService.CreateCustomerByBusinessStaffAsync(
                negocioId,
                requesterUserId.Value,
                User.IsInRole("Admin"),
                request,
                GetIpAddress(),
                GetUserAgent(),
                cancellationToken);

        return ToActionResult(result, data => StatusCode(StatusCodes.Status201Created, data));
    }

    [Authorize(Policy = "BusinessStaffAuth")]
    [HttpGet("customer-search")]
    public async Task<IActionResult> SearchCustomerByContact(
        [FromQuery] BusinessCustomerSearchRequest request,
        CancellationToken cancellationToken)
    {
        Guid? requesterUserId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!requesterUserId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<BusinessCustomerLookupResponse> result =
            await _userService.SearchBusinessCustomerByContactAsync(
                requesterUserId.Value,
                User.IsInRole("Admin"),
                request,
                cancellationToken);

        return ToActionResult(result, Ok);
    }

    [Authorize(Policy = "BusinessStaffAuth")]
    [HttpPost("my-businesses/{negocioId:guid}/customer-points/accrual")]
    public async Task<IActionResult> AccrueCustomerPointsByUserId(
        Guid negocioId,
        [FromBody] BusinessCustomerPointsAccrualRequest request,
        CancellationToken cancellationToken)
    {
        Guid? requesterUserId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!requesterUserId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<BusinessCustomerLookupResponse> targetResult =
            await _userService.GetBusinessCustomerByIdAsync(
                requesterUserId.Value,
                User.IsInRole("Admin"),
                request.UserId,
                cancellationToken);

        if (!targetResult.Succeeded)
        {
            return ToActionResult(targetResult, Ok);
        }

        Dynamic.Fidelity.Application.Common.ServiceResult<Dynamic.Fidelity.Application.DTOs.Responses.PointsEarnValidationResponse> result =
            await _pointsService.BackofficeAccrualByUserIdAsync(
                negocioId,
                requesterUserId.Value,
                User.IsInRole("Admin"),
                new BackofficeAccrualByUserIdRequest
                {
                    UserId = request.UserId,
                    AmountEuros = request.AmountEuros,
                    Reason = request.Reason,
                    Reference = request.Reference
                },
                cancellationToken);

        return ToActionResult(result, Ok);
    }

    private IActionResult ToActionResult<T>(ServiceResult<T> result, Func<T, IActionResult> onSuccess)
    {
        if (result.Succeeded && result.Data is not null)
        {
            return onSuccess(result.Data);
        }

        return result.ErrorCode switch
        {
            "validation_error" => BadRequest(new { message = result.ErrorMessage }),
            "conflict" => Conflict(new { message = result.ErrorMessage }),
            "not_found" => NotFound(new { message = result.ErrorMessage }),
            "forbidden" => StatusCode(StatusCodes.Status403Forbidden, new { message = result.ErrorMessage }),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new { message = result.ErrorMessage ?? "Error interno del servidor." })
        };
    }

    private IActionResult ToActionResult<T>(
        Dynamic.Fidelity.Application.Common.ServiceResult<T> result,
        Func<T, IActionResult> onSuccess)
    {
        if (result.Succeeded && result.Data is not null)
        {
            return onSuccess(result.Data);
        }

        return result.ErrorCode switch
        {
            "validation_error" => BadRequest(new { message = result.ErrorMessage }),
            "conflict" => Conflict(new { message = result.ErrorMessage }),
            "not_found" => NotFound(new { message = result.ErrorMessage }),
            "forbidden" => StatusCode(StatusCodes.Status403Forbidden, new { message = result.ErrorMessage }),
            "locked" => StatusCode(StatusCodes.Status423Locked, new { message = result.ErrorMessage }),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new { message = result.ErrorMessage ?? "Error interno del servidor." })
        };
    }

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

    private string? GetIpAddress()
        => HttpContext.Connection.RemoteIpAddress?.ToString();

    private string? GetUserAgent()
        => Request.Headers.UserAgent.FirstOrDefault();
}
