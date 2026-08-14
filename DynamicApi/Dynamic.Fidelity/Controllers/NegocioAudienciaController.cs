using System.Security.Claims;
using Dynamic.Fidelity.Application.Common;
using Dynamic.Fidelity.Application.Contracts.Services;
using Dynamic.Fidelity.Application.DTOs.Requests;
using Dynamic.Fidelity.Application.DTOs.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dynamic.Fidelity.Controllers;

[ApiController]
[Authorize]
[Route("api/negocios")]
public class NegocioAudienciaController : ControllerBase
{
    private readonly INegocioAudienciaService _negocioAudienciaService;

    public NegocioAudienciaController(INegocioAudienciaService negocioAudienciaService)
    {
        _negocioAudienciaService = negocioAudienciaService;
    }

    [HttpPost("{negocioId:guid}/audiencia")]
    public async Task<IActionResult> FormarParte(Guid negocioId, CancellationToken cancellationToken)
    {
        Guid? userId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<FormarParteNegocioResponse> result =
            await _negocioAudienciaService.FormarParteAsync(negocioId, userId.Value, cancellationToken);

        return ToActionResult(
            result,
            data => data.FormadoAhora
                ? StatusCode(StatusCodes.Status201Created, data)
                : Ok(data));
    }

    [HttpDelete("{negocioId:guid}/audiencia")]
    public async Task<IActionResult> DejarDeFormarParte(Guid negocioId, CancellationToken cancellationToken)
    {
        Guid? userId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult result =
            await _negocioAudienciaService.DejarDeFormarParteAsync(negocioId, userId.Value, cancellationToken);

        return ToActionResult(result, NoContent);
    }

    [HttpPatch("{negocioId:guid}/audiencia/favorito")]
    public async Task<IActionResult> SetFavorito(
        Guid negocioId,
        [FromBody] SetAudienceFavoriteRequest request,
        CancellationToken cancellationToken)
    {
        Guid? userId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<AudienceFavoriteResponse> result =
            await _negocioAudienciaService.SetFavoritoAsync(
                negocioId,
                userId.Value,
                request.EsFavorito,
                cancellationToken);

        return ToActionResult(result, Ok);
    }

    [HttpPost("{negocioId:guid}/audiencia/email/unsubscribe")]
    public async Task<IActionResult> UnsubscribeFromBusinessEmails(
        Guid negocioId,
        CancellationToken cancellationToken)
    {
        Guid? userId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<BusinessEmailPreferenceResponse> result =
            await _negocioAudienciaService.UnsubscribeFromBusinessEmailsAsync(
                negocioId,
                userId.Value,
                cancellationToken);

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
            "conflict" => Conflict(new { message = errorMessage }),
            "forbidden" => StatusCode(StatusCodes.Status403Forbidden, new { message = errorMessage }),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = errorMessage ?? "Error interno del servidor."
            })
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
