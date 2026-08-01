using System.Security.Claims;
using Dynamic.Promotions.Application.Contracts;
using Dynamic.Promotions.Application.DTOs.Requests;
using Dynamic.Promotions.Application.DTOs.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dynamic.Promotions.Controllers;

[ApiController]
[Authorize]
[Route("api/users/me/promotions")]
public class UserPromotionsController : ControllerBase
{
    private readonly IPromotionService _promotionService;

    public UserPromotionsController(IPromotionService promotionService)
    {
        _promotionService = promotionService;
    }

    [HttpGet]
    public async Task<IActionResult> GetReceived(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeRead = true,
        CancellationToken cancellationToken = default)
    {
        Guid? userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        ReceivedPromotionsPageResponse response = await _promotionService.GetReceivedPromotionsAsync(
            userId.Value,
            page,
            pageSize,
            includeRead,
            cancellationToken);

        return Ok(response);
    }

    [HttpPost("{recipientId:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid recipientId, CancellationToken cancellationToken)
    {
        Guid? userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        bool found = await _promotionService.MarkAsReadAsync(userId.Value, recipientId, cancellationToken);
        return found ? Ok(new { read = true }) : NotFound(new { message = "Promocion no encontrada." });
    }

    [HttpGet("unseen")]
    public async Task<IActionResult> GetUnseen(
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        Guid? userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        UnseenPromotionsResponse response = await _promotionService.GetUnseenPromotionsAsync(
            userId.Value,
            limit,
            cancellationToken);

        return Ok(response);
    }

    [HttpPost("presented")]
    public async Task<IActionResult> MarkAsPresented(
        [FromBody] MarkPromotionsAsPresentedRequest request,
        CancellationToken cancellationToken)
    {
        Guid? userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        Guid[] recipientIds = request.RecipientIds?.Distinct().ToArray() ?? [];
        if (recipientIds.Length is 0 or > 100)
        {
            return BadRequest(new { message = "Debes indicar entre 1 y 100 promociones." });
        }

        PresentedPromotionsResponse response = await _promotionService.MarkAsPresentedAsync(
            userId.Value,
            recipientIds,
            cancellationToken);

        return Ok(response);
    }

    private Guid? GetCurrentUserId()
    {
        string? value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out Guid userId) ? userId : null;
    }
}
