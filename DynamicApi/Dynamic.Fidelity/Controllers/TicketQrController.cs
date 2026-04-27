using Dynamic.Fidelity.Application.Common;
using Dynamic.Fidelity.Application.Contracts.Services;
using Dynamic.Fidelity.Application.DTOs.Requests;
using Dynamic.Fidelity.Application.DTOs.Responses;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dynamic.Fidelity.Controllers;

[ApiController]
[Route("api/fidelity/negocios/{negocioId:guid}/tickets")]
public class TicketQrController : ControllerBase
{
    private readonly ITicketQrService _ticketQrService;

    public TicketQrController(ITicketQrService ticketQrService)
    {
        _ticketQrService = ticketQrService;
    }

    [AllowAnonymous]
    [HttpGet("/api/fidelity/negocios/slug/{slugPortal}/tickets/by-qr")]
    public async Task<IActionResult> GetByQr(
        string slugPortal,
        [FromQuery(Name = "qr")] string qrToken,
        CancellationToken cancellationToken)
    {
        ServiceResult<TicketQrLookupResponse> result =
            await _ticketQrService.GetTicketByQrAsync(slugPortal, qrToken, cancellationToken);

        return ToActionResult(result, Ok);
    }

    [Authorize]
    [HttpPost("{ticketId:guid}/qr")]
    public async Task<IActionResult> GenerateQr(Guid negocioId, Guid ticketId, CancellationToken cancellationToken)
    {
        Guid? requesterUserId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!requesterUserId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<TicketQrResponse> result =
            await _ticketQrService.GenerateTicketQrAsync(
                negocioId,
                ticketId,
                requesterUserId.Value,
                User.IsInRole("Admin"),
                cancellationToken);

        return ToActionResult(result, data => StatusCode(StatusCodes.Status201Created, data));
    }

    [Authorize]
    [HttpPost("scan")]
    public async Task<IActionResult> ScanQr(
        Guid negocioId,
        [FromBody] ScanTicketQrRequest request,
        CancellationToken cancellationToken)
    {
        Guid? userId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<TicketQrScanResponse> result =
            await _ticketQrService.ScanTicketQrAsync(userId.Value, request.QrToken, cancellationToken);

        if (result.Succeeded && result.Data is not null && result.Data.NegocioId != negocioId)
        {
            return BadRequest(new { message = "El QR no pertenece al negocio indicado." });
        }

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
