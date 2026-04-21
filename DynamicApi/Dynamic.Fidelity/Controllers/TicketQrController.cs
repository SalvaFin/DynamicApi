using Dynamic.Fidelity.Application.Common;
using Dynamic.Fidelity.Application.Contracts.Services;
using Dynamic.Fidelity.Application.DTOs.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dynamic.Fidelity.Controllers;

[ApiController]
[Authorize(Policy = "AdminAuth")]
[Route("api/fidelity/negocios/{negocioId:guid}/tickets")]
public class TicketQrController : ControllerBase
{
    private readonly ITicketQrService _ticketQrService;

    public TicketQrController(ITicketQrService ticketQrService)
    {
        _ticketQrService = ticketQrService;
    }

    [HttpPost("{ticketId:guid}/qr")]
    public async Task<IActionResult> GenerateQr(Guid negocioId, Guid ticketId, CancellationToken cancellationToken)
    {
        ServiceResult<TicketQrResponse> result =
            await _ticketQrService.GenerateTicketQrAsync(negocioId, ticketId, cancellationToken);

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
            "not_found" => NotFound(new { message = result.ErrorMessage }),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new { message = result.ErrorMessage ?? "Error interno del servidor." })
        };
    }
}
