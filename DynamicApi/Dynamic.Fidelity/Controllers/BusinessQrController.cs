using System.Security.Claims;
using Dynamic.Fidelity.Application.Common;
using Dynamic.Fidelity.Application.Contracts.Services;
using Dynamic.Fidelity.Application.DTOs.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dynamic.Fidelity.Controllers;

[ApiController]
[Authorize(Policy = "BusinessStaffAuth")]
[Route("api/backoffice/negocios/{negocioId:guid}/qr")]
public class BusinessQrController : ControllerBase
{
    private readonly IBusinessQrService _businessQrService;

    public BusinessQrController(IBusinessQrService businessQrService)
    {
        _businessQrService = businessQrService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(Guid negocioId, CancellationToken cancellationToken)
    {
        Guid? requesterUserId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!requesterUserId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<BusinessQrResponse> result = await _businessQrService.GenerateAsync(
            negocioId,
            requesterUserId.Value,
            User.IsInRole("Admin"),
            cancellationToken);

        if (result.Succeeded && result.Data is not null)
        {
            return Ok(result.Data);
        }

        return result.ErrorCode switch
        {
            "validation_error" => BadRequest(new { message = result.ErrorMessage }),
            "not_found" => NotFound(new { message = result.ErrorMessage }),
            "forbidden" => StatusCode(StatusCodes.Status403Forbidden, new { message = result.ErrorMessage }),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new { message = result.ErrorMessage ?? "Error interno del servidor." })
        };
    }

    private Guid? GetClaimGuid(params string[] claimTypes)
    {
        foreach (string claimType in claimTypes)
        {
            if (Guid.TryParse(User.FindFirstValue(claimType), out Guid userId))
            {
                return userId;
            }
        }

        return null;
    }
}
