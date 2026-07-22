using System.Security.Claims;
using Dynamic.Reports.Application.Common;
using Dynamic.Reports.Application.Contracts;
using Dynamic.Reports.Application.DTOs.Requests;
using Dynamic.Reports.Application.DTOs.Responses;
using Dynamic.Reports.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dynamic.Reports.Controllers;

[ApiController]
[Authorize]
[Route("api/users/me/reports")]
public sealed class UserReportsController : ControllerBase
{
    private const int MaxPageSize = 100;
    private readonly IReportService _reportService;

    public UserReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("options")]
    public ActionResult<ReportOptionsResponse> GetOptions()
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(_reportService.GetOptions());
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReportRequest request, CancellationToken cancellationToken)
    {
        Guid? userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        ReportResult<ReportDetailResponse> result = await _reportService.CreateAsync(userId.Value, request, cancellationToken);
        return ToActionResult(result, data => CreatedAtAction(nameof(GetById), new { reportId = data.Id }, data));
    }

    [HttpGet]
    public async Task<IActionResult> GetMine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] ReportStatus? status = null,
        [FromQuery] ReportCategory? category = null,
        CancellationToken cancellationToken = default)
    {
        Guid? userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        Response.Headers.CacheControl = "no-store";
        return Ok(await _reportService.GetMineAsync(
            userId.Value, Math.Max(1, page), Math.Clamp(pageSize, 1, MaxPageSize), status, category, cancellationToken));
    }

    [HttpGet("{reportId:guid}")]
    public async Task<IActionResult> GetById(Guid reportId, CancellationToken cancellationToken)
    {
        Guid? userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        Response.Headers.CacheControl = "no-store";
        ReportResult<ReportDetailResponse> result = await _reportService.GetMineByIdAsync(userId.Value, reportId, cancellationToken);
        return ToActionResult(result, Ok);
    }

    [HttpPost("{reportId:guid}/messages")]
    public async Task<IActionResult> AddMessage(
        Guid reportId,
        [FromBody] AddReportMessageRequest request,
        CancellationToken cancellationToken)
    {
        Guid? userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        ReportResult<ReportDetailResponse> result = await _reportService.AddUserMessageAsync(userId.Value, reportId, request, cancellationToken);
        return ToActionResult(result, Ok);
    }

    private Guid? GetCurrentUserId()
    {
        string? value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out Guid userId) ? userId : null;
    }

    private IActionResult ToActionResult<T>(ReportResult<T> result, Func<T, IActionResult> onSuccess)
    {
        if (result.Succeeded && result.Data is not null) return onSuccess(result.Data);
        return result.ErrorCode switch
        {
            "validation_error" => BadRequest(new { message = result.ErrorMessage }),
            "not_found" => NotFound(new { message = result.ErrorMessage }),
            "conflict" => Conflict(new { message = result.ErrorMessage }),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new { message = result.ErrorMessage ?? "Error interno del servidor." })
        };
    }
}
