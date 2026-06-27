using System.Security.Claims;
using Dynamic.Promotions.Application.Contracts;
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

    private Guid? GetCurrentUserId()
    {
        string? value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out Guid userId) ? userId : null;
    }
}
