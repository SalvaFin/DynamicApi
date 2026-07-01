using System.Security.Claims;
using Dynamic.Promotions.Application.Common;
using Dynamic.Promotions.Application.Contracts;
using Dynamic.Promotions.Application.DTOs.Requests;
using Dynamic.Promotions.Application.DTOs.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dynamic.Promotions.Controllers;

[ApiController]
[Authorize(Roles = "Admin,PropietarioNegocio")]
[Route("api/promotions/negocios/{negocioId:guid}/campaigns")]
public class BusinessPromotionsController : ControllerBase
{
    private readonly IPromotionService _promotionService;

    public BusinessPromotionsController(IPromotionService promotionService)
    {
        _promotionService = promotionService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        Guid negocioId,
        [FromBody] CreatePromotionCampaignRequest request,
        CancellationToken cancellationToken)
    {
        Guid? userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        PromotionServiceResult<PromotionCampaignResponse> result = await _promotionService.CreateCampaignAsync(
            negocioId,
            userId.Value,
            User.IsInRole("Admin"),
            request,
            cancellationToken);

        return result.Succeeded && result.Data is not null
            ? AcceptedAtAction(nameof(GetById), new { negocioId, campaignId = result.Data.Id }, result.Data)
            : MapFailure(result);
    }

    [HttpPost("audience-preview")]
    public async Task<IActionResult> PreviewAudience(
        Guid negocioId,
        [FromBody] PromotionAudiencePreviewRequest request,
        CancellationToken cancellationToken)
    {
        Guid? userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        PromotionServiceResult<PromotionAudiencePreviewResponse> result = await _promotionService.PreviewAudienceAsync(
            negocioId,
            userId.Value,
            User.IsInRole("Admin"),
            request,
            cancellationToken);

        return result.Succeeded && result.Data is not null ? Ok(result.Data) : MapFailure(result);
    }

    [HttpGet("{campaignId:guid}")]
    public async Task<IActionResult> GetById(
        Guid negocioId,
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        Guid? userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        PromotionServiceResult<PromotionCampaignResponse> result = await _promotionService.GetCampaignAsync(
            negocioId,
            campaignId,
            userId.Value,
            User.IsInRole("Admin"),
            cancellationToken);

        return result.Succeeded && result.Data is not null ? Ok(result.Data) : MapFailure(result);
    }

    private Guid? GetCurrentUserId()
    {
        string? value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out Guid userId) ? userId : null;
    }

    private IActionResult MapFailure<T>(PromotionServiceResult<T> result)
        => result.ErrorCode switch
        {
            "validation_error" => BadRequest(new { code = result.ErrorCode, message = result.ErrorMessage }),
            "forbidden" => StatusCode(StatusCodes.Status403Forbidden, new { code = result.ErrorCode, message = result.ErrorMessage }),
            "not_found" => NotFound(new { code = result.ErrorCode, message = result.ErrorMessage }),
            "conflict" => Conflict(new { code = result.ErrorCode, message = result.ErrorMessage }),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new { code = "server_error", message = result.ErrorMessage })
        };
}
