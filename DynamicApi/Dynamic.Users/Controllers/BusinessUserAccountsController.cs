using System.Security.Claims;
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

    public BusinessUserAccountsController(IBusinessUserProvisioningService businessUserProvisioningService)
    {
        _businessUserProvisioningService = businessUserProvisioningService;
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
