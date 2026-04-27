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
[Route("api/fidelity/negocios/{negocioId:guid}/tickets")]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _ticketService;

    public TicketsController(ITicketService ticketService)
    {
        _ticketService = ticketService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid negocioId, CancellationToken cancellationToken)
    {
        Guid? requesterUserId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!requesterUserId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<IReadOnlyCollection<TicketResponse>> result =
            await _ticketService.GetAllAsync(negocioId, requesterUserId.Value, User.IsInRole("Admin"), cancellationToken);

        return ToActionResult(result, Ok);
    }

    [HttpGet("{ticketId:guid}")]
    public async Task<IActionResult> GetById(Guid negocioId, Guid ticketId, CancellationToken cancellationToken)
    {
        Guid? requesterUserId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!requesterUserId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<TicketResponse> result =
            await _ticketService.GetByIdAsync(negocioId, ticketId, requesterUserId.Value, User.IsInRole("Admin"), cancellationToken);

        return ToActionResult(result, Ok);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        Guid negocioId,
        [FromBody] CreateTicketRequest request,
        CancellationToken cancellationToken)
    {
        Guid? requesterUserId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!requesterUserId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<TicketResponse> result =
            await _ticketService.CreateAsync(negocioId, requesterUserId.Value, User.IsInRole("Admin"), request, cancellationToken);

        return ToActionResult(result, data => StatusCode(StatusCodes.Status201Created, data));
    }

    [HttpPut("{ticketId:guid}")]
    public async Task<IActionResult> Update(
        Guid negocioId,
        Guid ticketId,
        [FromBody] UpdateTicketRequest request,
        CancellationToken cancellationToken)
    {
        Guid? requesterUserId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!requesterUserId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<TicketResponse> result =
            await _ticketService.UpdateAsync(negocioId, ticketId, requesterUserId.Value, User.IsInRole("Admin"), request, cancellationToken);

        return ToActionResult(result, Ok);
    }

    [HttpPost("{ticketId:guid}/unlock")]
    public async Task<IActionResult> Unlock(Guid negocioId, Guid ticketId, CancellationToken cancellationToken)
    {
        Guid? requesterUserId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!requesterUserId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<TicketResponse> result =
            await _ticketService.UnlockAsync(negocioId, ticketId, requesterUserId.Value, cancellationToken);

        return ToActionResult(result, data => StatusCode(StatusCodes.Status201Created, data));
    }

    [HttpDelete("{ticketId:guid}")]
    public async Task<IActionResult> Delete(Guid negocioId, Guid ticketId, CancellationToken cancellationToken)
    {
        Guid? requesterUserId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!requesterUserId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult result =
            await _ticketService.DeleteAsync(negocioId, ticketId, requesterUserId.Value, User.IsInRole("Admin"), cancellationToken);

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
            "not_found" => NotFound(new { message = errorMessage }),
            "conflict" => Conflict(new { message = errorMessage }),
            "insufficient_balance" => BadRequest(new { message = errorMessage }),
            "forbidden" => StatusCode(StatusCodes.Status403Forbidden, new { message = errorMessage }),
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
