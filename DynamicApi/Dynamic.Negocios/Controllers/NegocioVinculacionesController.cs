using System.Security.Claims;
using Dynamic.Negocios.Application.Common;
using Dynamic.Negocios.Application.Contracts.Services;
using Dynamic.Negocios.Application.DTOs.Requests;
using Dynamic.Negocios.Application.DTOs.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dynamic.Negocios.Controllers;

[ApiController]
[Route("api/negocios")]
public class NegocioVinculacionesController : ControllerBase
{
    private readonly INegocioUsuarioVinculacionService _negocioUsuarioVinculacionService;

    public NegocioVinculacionesController(INegocioUsuarioVinculacionService negocioUsuarioVinculacionService)
    {
        _negocioUsuarioVinculacionService = negocioUsuarioVinculacionService;
    }

    [Authorize]
    [HttpGet("mis-negocios")]
    public async Task<IActionResult> GetMisNegocios(CancellationToken cancellationToken)
    {
        Guid? userId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<IReadOnlyCollection<NegocioVinculadoResponse>> result =
            await _negocioUsuarioVinculacionService.GetNegociosByUserAsync(userId.Value, cancellationToken);

        return ToActionResult(result, Ok);
    }

    [Authorize(Policy = "AdminAuth")]
    [HttpPost("{negocioId:guid}/usuarios/{userId:guid}/vinculaciones")]
    public async Task<IActionResult> LinkUser(
        Guid negocioId,
        Guid userId,
        [FromBody] VincularUsuarioNegocioRequest request,
        CancellationToken cancellationToken)
    {
        ServiceResult<NegocioUsuarioVinculacionResponse> result =
            await _negocioUsuarioVinculacionService.LinkUserAsync(
                negocioId,
                userId,
                request,
                GetClaimGuid(ClaimTypes.NameIdentifier, "sub"),
                cancellationToken);

        return ToActionResult(result, data => StatusCode(StatusCodes.Status201Created, data));
    }

    [Authorize(Policy = "AdminAuth")]
    [HttpDelete("{negocioId:guid}/usuarios/{userId:guid}/vinculaciones")]
    public async Task<IActionResult> UnlinkUser(Guid negocioId, Guid userId, CancellationToken cancellationToken)
    {
        ServiceResult result = await _negocioUsuarioVinculacionService.UnlinkUserAsync(
            negocioId,
            userId,
            GetClaimGuid(ClaimTypes.NameIdentifier, "sub"),
            cancellationToken);

        return ToActionResult(result, NoContent);
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
            "conflict" => Conflict(new { message = errorMessage }),
            "not_found" => NotFound(new { message = errorMessage }),
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
