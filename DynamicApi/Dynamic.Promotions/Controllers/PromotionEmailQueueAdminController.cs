using Dynamic.Promotions.Application.Models;
using Dynamic.Promotions.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dynamic.Promotions.Controllers;

[ApiController]
[Authorize(Policy = "AdminAuth")]
[Route("api/admin/promotions/email-queue")]
public sealed class PromotionEmailQueueAdminController : ControllerBase
{
    private readonly PromotionEmailQueueTelemetry _telemetry;

    public PromotionEmailQueueAdminController(PromotionEmailQueueTelemetry telemetry)
    {
        _telemetry = telemetry;
    }

    [HttpGet]
    [ProducesResponseType<PromotionEmailQueueSnapshot>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult<PromotionEmailQueueSnapshot> Get()
    {
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        return Ok(_telemetry.GetSnapshot());
    }
}
