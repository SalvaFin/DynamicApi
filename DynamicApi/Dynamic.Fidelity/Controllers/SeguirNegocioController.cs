using System.Security.Claims;
using Dynamic.Fidelity.Application.Common;
using Dynamic.Fidelity.Application.Contracts.Services;
using Dynamic.Fidelity.Application.DTOs.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dynamic.Fidelity.Controllers;

[ApiController]
[Authorize]
[Route("api/negocios")]
public class SeguirNegocioController : ControllerBase
{
    private readonly ISeguirNegocioService _seguirNegocioService;

    public SeguirNegocioController(ISeguirNegocioService seguirNegocioService)
    {
        _seguirNegocioService = seguirNegocioService;
    }

    [HttpPost("{negocioId:guid}/seguir")]
    public async Task<IActionResult> Seguir(Guid negocioId, CancellationToken cancellationToken)
    {
        Guid? userId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<SeguirNegocioResponse> result =
            await _seguirNegocioService.SeguirAsync(negocioId, userId.Value, cancellationToken);

        if (!result.Succeeded || result.Data is null)
        {
            return result.ErrorCode switch
            {
                "validation_error" => BadRequest(new { message = result.ErrorMessage }),
                "not_found" => NotFound(new { message = result.ErrorMessage }),
                "conflict" => Conflict(new { message = result.ErrorMessage }),
                _ => StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = result.ErrorMessage ?? "Error interno del servidor."
                })
            };
        }

        return result.Data.VinculadoAhora
            ? StatusCode(StatusCodes.Status201Created, result.Data)
            : Ok(result.Data);
    }

    [HttpDelete("{negocioId:guid}/seguir")]
    public async Task<IActionResult> DejarDeSeguir(Guid negocioId, CancellationToken cancellationToken)
    {
        Guid? userId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult result =
            await _seguirNegocioService.DejarDeSeguirAsync(negocioId, userId.Value, cancellationToken);

        if (result.Succeeded)
        {
            return NoContent();
        }

        return result.ErrorCode switch
        {
            "validation_error" => BadRequest(new { message = result.ErrorMessage }),
            "not_found" => NotFound(new { message = result.ErrorMessage }),
            "forbidden" => StatusCode(StatusCodes.Status403Forbidden, new { message = result.ErrorMessage }),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = result.ErrorMessage ?? "Error interno del servidor."
            })
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
