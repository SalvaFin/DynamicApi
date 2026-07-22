using Dynamic.Reports.Application.Common;
using Dynamic.Reports.Application.DTOs.Requests;
using Dynamic.Reports.Application.DTOs.Responses;
using Dynamic.Reports.Domain.Enums;

namespace Dynamic.Reports.Application.Contracts;

public interface IReportService
{
    ReportOptionsResponse GetOptions();
    Task<ReportResult<ReportDetailResponse>> CreateAsync(Guid userId, CreateReportRequest request, CancellationToken cancellationToken);
    Task<PaginatedReportResponse<ReportSummaryResponse>> GetMineAsync(Guid userId, int page, int pageSize, ReportStatus? status, ReportCategory? category, CancellationToken cancellationToken);
    Task<ReportResult<ReportDetailResponse>> GetMineByIdAsync(Guid userId, Guid reportId, CancellationToken cancellationToken);
    Task<ReportResult<ReportDetailResponse>> AddUserMessageAsync(Guid userId, Guid reportId, AddReportMessageRequest request, CancellationToken cancellationToken);
    Task<PaginatedReportResponse<ReportSummaryResponse>> GetAdminListAsync(int page, int pageSize, ReportStatus? status, ReportPriority? priority, ReportCategory? category, Guid? assignedAdminUserId, bool? unassigned, string? search, CancellationToken cancellationToken);
    Task<AdminReportDashboardResponse> GetAdminDashboardAsync(CancellationToken cancellationToken);
    Task<ReportResult<ReportDetailResponse>> GetAdminByIdAsync(Guid reportId, CancellationToken cancellationToken);
    Task<ReportResult<ReportDetailResponse>> UpdateByAdminAsync(Guid adminUserId, Guid reportId, AdminUpdateReportRequest request, CancellationToken cancellationToken);
}
