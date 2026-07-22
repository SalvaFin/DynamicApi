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
[Authorize(Policy = "AdminAuth")]
[Route("api/admin/reports")]
public sealed class AdminReportsController : ControllerBase
{
    private const int MaxPageSize = 100;
    private readonly IReportService _reportService;

    public AdminReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("options")]
    public ActionResult<ReportOptionsResponse> GetOptions()
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(_reportService.GetOptions());
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await _reportService.GetAdminDashboardAsync(cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] ReportStatus? status = null,
        [FromQuery] ReportPriority? priority = null,
        [FromQuery] ReportCategory? category = null,
        [FromQuery] Guid? assignedAdminUserId = null,
        [FromQuery] bool? unassigned = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await _reportService.GetAdminListAsync(
            Math.Max(1, page), Math.Clamp(pageSize, 1, MaxPageSize), status, priority, category,
            assignedAdminUserId, unassigned, search, cancellationToken));
    }

    [HttpGet("{reportId:guid}")]
    public async Task<IActionResult> GetById(Guid reportId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        ReportResult<ReportDetailResponse> result = await _reportService.GetAdminByIdAsync(reportId, cancellationToken);
        return ToActionResult(result, Ok);
    }

    [HttpPatch("{reportId:guid}")]
    public async Task<IActionResult> Update(
        Guid reportId,
        [FromBody] AdminUpdateReportRequest request,
        CancellationToken cancellationToken)
    {
        Guid? adminUserId = GetCurrentUserId();
        if (!adminUserId.HasValue) return Unauthorized();

        ReportResult<ReportDetailResponse> result = await _reportService.UpdateByAdminAsync(
            adminUserId.Value, reportId, request, cancellationToken);
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
