using System.Security.Claims;
using Dynamic.Users.Application.DTOs.Requests;
using Dynamic.Users.Application.DTOs.Responses;
using Dynamic.Users.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dynamic.Users.Controllers;

[ApiController]
[Authorize(Roles = "User")]
[Route("api/users/me/points-splits")]
public class PointsSplitController : ControllerBase
{
    private readonly PointsSplitService _service;

    public PointsSplitController(PointsSplitService service)
    {
        _service = service;
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> GetRecentEarnings(CancellationToken cancellationToken)
    {
        Guid? userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        return Ok(await _service.GetRecentEarningsAsync(userId.Value, cancellationToken));
    }

    [HttpPost("resolve-recipient")]
    [EnableRateLimiting("points-split-identity")]
    public async Task<IActionResult> ResolveRecipient(
        [FromBody] ResolveSplitRecipientRequest request, CancellationToken cancellationToken)
    {
        Guid? requesterId = CurrentUserId();
        if (requesterId is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Email) == string.IsNullOrWhiteSpace(request.CustomerQrPayload))
            return BadRequest(new { message = "Introduce un correo o escanea el QR habitual de un cliente." });

        SplitRecipientResponse? recipient;
        if (!string.IsNullOrWhiteSpace(request.CustomerQrPayload))
        {
            // UserPortalController generates the normal customer QR with their user ID.
            if (!Guid.TryParse(request.CustomerQrPayload.Trim(), out Guid userId) || userId == Guid.Empty)
                return BadRequest(new { message = "Este QR no es un QR de cliente válido." });
            recipient = await _service.ResolveByUserIdAsync(userId, cancellationToken);
        }
        else
        {
            recipient = await _service.ResolveByEmailAsync(request.Email!, cancellationToken);
        }

        if (recipient is null)
            return NotFound(new { message = "Usuario no encontrado." });
        if (recipient.UserId == requesterId)
            return BadRequest(new { message = "Tu cuenta ya participa en el reparto." });
        return Ok(recipient);
    }

    [HttpGet("transactions/{sourceTransactionId:guid}")]
    public async Task<IActionResult> GetSplit(Guid sourceTransactionId, CancellationToken cancellationToken)
    {
        Guid? userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        PointsSplitResponse? split = await _service.GetAsync(userId.Value, sourceTransactionId, cancellationToken);
        return split is null ? NotFound() : Ok(split);
    }

    [HttpGet("transactions/{sourceTransactionId:guid}/status")]
    public async Task<IActionResult> GetStatus(Guid sourceTransactionId, CancellationToken cancellationToken)
    {
        Guid? userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        PointsSplitStatusResponse? status = await _service.GetStatusAsync(userId.Value, sourceTransactionId, cancellationToken);
        return status is null ? NotFound() : Ok(status);
    }

    [HttpPost("transactions/{sourceTransactionId:guid}")]
    [EnableRateLimiting("points-split-create")]
    public async Task<IActionResult> CreateSplit(
        Guid sourceTransactionId, [FromBody] CreatePointsSplitRequest request,
        CancellationToken cancellationToken)
    {
        Guid? userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        (PointsSplitResponse? split, string? error) = await _service.CreateAsync(
            userId.Value, sourceTransactionId, request.RecipientEmails, cancellationToken);
        if (split is null)
            return error?.Contains("ya se ha repartido") == true
                ? Conflict(new { message = error })
                : BadRequest(new { message = error });
        return CreatedAtAction(nameof(GetSplit), new { sourceTransactionId }, split);
    }

    private Guid? CurrentUserId()
    {
        string? value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out Guid id) ? id : null;
    }
}
