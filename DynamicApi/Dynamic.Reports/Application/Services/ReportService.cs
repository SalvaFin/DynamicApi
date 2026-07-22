using Dynamic.Fidelity.Infrastructure.Persistence;
using Dynamic.Negocios.Infrastructure.Persistence;
using Dynamic.Promotions.Infrastructure.Persistence;
using Dynamic.Reports.Application.Common;
using Dynamic.Reports.Application.Contracts;
using Dynamic.Reports.Application.DTOs.Requests;
using Dynamic.Reports.Application.DTOs.Responses;
using Dynamic.Reports.Domain.Entities;
using Dynamic.Reports.Domain.Enums;
using Dynamic.Reports.Infrastructure.Persistence;
using Dynamic.Users.Domain.Entities;
using Dynamic.Users.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dynamic.Reports.Application.Services;

public sealed class ReportService : IReportService
{
    private readonly DynamicReportsDbContext _reportsDbContext;
    private readonly DynamicUsersDbContext _usersDbContext;
    private readonly DynamicFidelityDbContext _fidelityDbContext;
    private readonly DynamicNegociosDbContext _negociosDbContext;
    private readonly DynamicPromotionsDbContext _promotionsDbContext;

    public ReportService(
        DynamicReportsDbContext reportsDbContext,
        DynamicUsersDbContext usersDbContext,
        DynamicFidelityDbContext fidelityDbContext,
        DynamicNegociosDbContext negociosDbContext,
        DynamicPromotionsDbContext promotionsDbContext)
    {
        _reportsDbContext = reportsDbContext;
        _usersDbContext = usersDbContext;
        _fidelityDbContext = fidelityDbContext;
        _negociosDbContext = negociosDbContext;
        _promotionsDbContext = promotionsDbContext;
    }

    public ReportOptionsResponse GetOptions() => new()
    {
        Categories =
        [
            Category(ReportCategory.TicketLost, "He perdido un ticket", "El ticket ya no aparece o no puedes localizarlo.", ticket: true, business: true),
            Category(ReportCategory.TicketNotReceived, "No he recibido un ticket", "Esperabas recibir un ticket y no llegó a tu cuenta.", ticket: true, business: true, promotion: true),
            Category(ReportCategory.TicketIncorrectlyRedeemed, "Ticket canjeado incorrectamente", "El ticket figura como usado pero no reconoces el canje.", ticket: true, business: true),
            Category(ReportCategory.TicketOther, "Otro problema con un ticket", "Caducidad, condiciones, visualización u otro problema relacionado.", ticket: true, business: true),
            Category(ReportCategory.PointsBalance, "Problema con mis puntos", "El saldo o un movimiento de puntos no es correcto.", business: true),
            Category(ReportCategory.Promotion, "Problema con una promoción", "Recepción, contenido o funcionamiento de una promoción.", business: true, promotion: true),
            Category(ReportCategory.QrScan, "Problema al escanear un QR", "El QR no funciona, no se reconoce o produce un resultado inesperado.", ticket: true, business: true),
            Category(ReportCategory.AccountAccess, "Acceso a mi cuenta", "Problemas de inicio de sesión, bloqueo o métodos de acceso.", business: false),
            Category(ReportCategory.AccountData, "Datos de mi cuenta", "Datos personales incorrectos o que no puedes modificar.", business: false),
            Category(ReportCategory.BusinessInformation, "Información de un negocio", "Los datos públicos de un negocio son incorrectos.", business: true),
            Category(ReportCategory.BusinessExperience, "Incidencia con un negocio", "Has tenido un problema relacionado con un negocio de la plataforma.", business: true),
            Category(ReportCategory.Other, "Otro problema", "Cualquier incidencia que no encaje en las opciones anteriores.", business: true)
        ],
        Statuses = Enum.GetValues<ReportStatus>(),
        Priorities = Enum.GetValues<ReportPriority>()
    };

    public async Task<ReportResult<ReportDetailResponse>> CreateAsync(
        Guid userId,
        CreateReportRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Category))
        {
            return ReportResult<ReportDetailResponse>.Failure("validation_error", "La categoría del reporte no es válida.");
        }

        if (string.IsNullOrWhiteSpace(request.Subject) || request.Subject.Trim().Length < 5)
        {
            return ReportResult<ReportDetailResponse>.Failure("validation_error", "El asunto debe tener al menos 5 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Trim().Length < 10)
        {
            return ReportResult<ReportDetailResponse>.Failure("validation_error", "La descripción debe tener al menos 10 caracteres.");
        }

        if (request.OccurredAtUtc > DateTime.UtcNow.AddMinutes(5))
        {
            return ReportResult<ReportDetailResponse>.Failure("validation_error", "La fecha de la incidencia no puede estar en el futuro.");
        }

        Guid? businessId = request.BusinessId;

        if (request.TicketId.HasValue)
        {
            var ticket = await _fidelityDbContext.Tickets
                .AsNoTracking()
                .Where(item => item.Id == request.TicketId.Value && item.UserId == userId)
                .Select(item => new { item.NegocioId })
                .FirstOrDefaultAsync(cancellationToken);

            if (ticket is null)
            {
                return ReportResult<ReportDetailResponse>.Failure("not_found", "El ticket indicado no existe o no pertenece al usuario.");
            }

            if (businessId.HasValue && businessId.Value != ticket.NegocioId)
            {
                return ReportResult<ReportDetailResponse>.Failure("validation_error", "El ticket no pertenece al negocio indicado.");
            }

            businessId ??= ticket.NegocioId;
        }

        if (request.PromotionCampaignId.HasValue)
        {
            var promotion = await _promotionsDbContext.Recipients
                .AsNoTracking()
                .Where(recipient => recipient.CampaignId == request.PromotionCampaignId.Value && recipient.UserId == userId)
                .Select(recipient => new { recipient.Campaign.NegocioId })
                .FirstOrDefaultAsync(cancellationToken);

            if (promotion is null)
            {
                return ReportResult<ReportDetailResponse>.Failure("not_found", "La promoción indicada no fue enviada al usuario.");
            }

            if (businessId.HasValue && businessId.Value != promotion.NegocioId)
            {
                return ReportResult<ReportDetailResponse>.Failure("validation_error", "La promoción no pertenece al negocio indicado.");
            }

            businessId ??= promotion.NegocioId;
        }

        if (businessId.HasValue && !await _negociosDbContext.Negocios.AsNoTracking()
                .AnyAsync(business => business.Id == businessId.Value, cancellationToken))
        {
            return ReportResult<ReportDetailResponse>.Failure("not_found", "El negocio indicado no existe.");
        }

        DateTime now = DateTime.UtcNow;
        SupportReport report = new()
        {
            Id = Guid.NewGuid(),
            ReporterUserId = userId,
            Category = request.Category,
            Status = ReportStatus.Open,
            Priority = ReportPriority.Normal,
            Subject = request.Subject.Trim(),
            Description = request.Description.Trim(),
            TicketId = request.TicketId,
            BusinessId = businessId,
            PromotionCampaignId = request.PromotionCampaignId,
            OccurredAtUtc = request.OccurredAtUtc,
            PageUrl = NullIfWhiteSpace(request.PageUrl),
            AppVersion = NullIfWhiteSpace(request.AppVersion),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Events =
            [
                new ReportEvent
                {
                    Id = Guid.NewGuid(),
                    ActorUserId = userId,
                    Kind = ReportEventKind.Created,
                    IsInternal = false,
                    NewStatus = ReportStatus.Open,
                    CreatedAtUtc = now
                }
            ]
        };

        _reportsDbContext.Reports.Add(report);
        await _reportsDbContext.SaveChangesAsync(cancellationToken);

        return ReportResult<ReportDetailResponse>.Success(await MapDetailAsync(report, includeInternal: false, cancellationToken));
    }

    public async Task<PaginatedReportResponse<ReportSummaryResponse>> GetMineAsync(
        Guid userId,
        int page,
        int pageSize,
        ReportStatus? status,
        ReportCategory? category,
        CancellationToken cancellationToken)
    {
        IQueryable<SupportReport> query = _reportsDbContext.Reports.AsNoTracking()
            .Where(report => report.ReporterUserId == userId);

        query = ApplyFilters(query, status, priority: null, category, assignedAdminUserId: null, unassigned: null, search: null);
        return await PaginateAsync(query, page, pageSize, cancellationToken);
    }

    public async Task<ReportResult<ReportDetailResponse>> GetMineByIdAsync(
        Guid userId,
        Guid reportId,
        CancellationToken cancellationToken)
    {
        SupportReport? report = await _reportsDbContext.Reports
            .AsNoTracking()
            .Include(item => item.Events)
            .FirstOrDefaultAsync(item => item.Id == reportId && item.ReporterUserId == userId, cancellationToken);

        return report is null
            ? ReportResult<ReportDetailResponse>.Failure("not_found", "Reporte no encontrado.")
            : ReportResult<ReportDetailResponse>.Success(await MapDetailAsync(report, includeInternal: false, cancellationToken));
    }

    public async Task<ReportResult<ReportDetailResponse>> AddUserMessageAsync(
        Guid userId,
        Guid reportId,
        AddReportMessageRequest request,
        CancellationToken cancellationToken)
    {
        SupportReport? report = await _reportsDbContext.Reports
            .Include(item => item.Events)
            .FirstOrDefaultAsync(item => item.Id == reportId && item.ReporterUserId == userId, cancellationToken);

        if (report is null)
        {
            return ReportResult<ReportDetailResponse>.Failure("not_found", "Reporte no encontrado.");
        }

        if (report.Status is ReportStatus.Resolved or ReportStatus.Rejected)
        {
            return ReportResult<ReportDetailResponse>.Failure("conflict", "No se pueden añadir mensajes a un reporte cerrado.");
        }

        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Trim().Length < 2)
        {
            return ReportResult<ReportDetailResponse>.Failure("validation_error", "El mensaje debe tener al menos 2 caracteres.");
        }

        DateTime now = DateTime.UtcNow;
        report.Events.Add(new ReportEvent
        {
            Id = Guid.NewGuid(),
            ActorUserId = userId,
            Kind = ReportEventKind.UserMessage,
            Message = request.Message.Trim(),
            CreatedAtUtc = now
        });

        if (report.Status == ReportStatus.WaitingForUser)
        {
            report.Events.Add(StatusEvent(report, userId, ReportStatus.InReview, now));
            report.Status = ReportStatus.InReview;
        }

        report.UpdatedAtUtc = now;
        await _reportsDbContext.SaveChangesAsync(cancellationToken);
        return ReportResult<ReportDetailResponse>.Success(await MapDetailAsync(report, includeInternal: false, cancellationToken));
    }

    public async Task<PaginatedReportResponse<ReportSummaryResponse>> GetAdminListAsync(
        int page,
        int pageSize,
        ReportStatus? status,
        ReportPriority? priority,
        ReportCategory? category,
        Guid? assignedAdminUserId,
        bool? unassigned,
        string? search,
        CancellationToken cancellationToken)
    {
        IQueryable<SupportReport> query = ApplyFilters(
            _reportsDbContext.Reports.AsNoTracking(), status, priority, category,
            assignedAdminUserId, unassigned, search);

        return await PaginateAsync(query, page, pageSize, cancellationToken);
    }

    public async Task<AdminReportDashboardResponse> GetAdminDashboardAsync(CancellationToken cancellationToken)
    {
        Dictionary<ReportStatus, int> byStatus = await _reportsDbContext.Reports.AsNoTracking()
            .GroupBy(report => report.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Status, item => item.Count, cancellationToken);

        return new AdminReportDashboardResponse
        {
            Open = byStatus.GetValueOrDefault(ReportStatus.Open),
            InReview = byStatus.GetValueOrDefault(ReportStatus.InReview),
            WaitingForUser = byStatus.GetValueOrDefault(ReportStatus.WaitingForUser),
            Resolved = byStatus.GetValueOrDefault(ReportStatus.Resolved),
            Rejected = byStatus.GetValueOrDefault(ReportStatus.Rejected),
            Unassigned = await _reportsDbContext.Reports.AsNoTracking()
                .CountAsync(report => report.AssignedAdminUserId == null &&
                    report.Status != ReportStatus.Resolved && report.Status != ReportStatus.Rejected, cancellationToken),
            Critical = await _reportsDbContext.Reports.AsNoTracking()
                .CountAsync(report => report.Priority == ReportPriority.Critical &&
                    report.Status != ReportStatus.Resolved && report.Status != ReportStatus.Rejected, cancellationToken)
        };
    }

    public async Task<ReportResult<ReportDetailResponse>> GetAdminByIdAsync(Guid reportId, CancellationToken cancellationToken)
    {
        SupportReport? report = await _reportsDbContext.Reports.AsNoTracking()
            .Include(item => item.Events)
            .FirstOrDefaultAsync(item => item.Id == reportId, cancellationToken);

        return report is null
            ? ReportResult<ReportDetailResponse>.Failure("not_found", "Reporte no encontrado.")
            : ReportResult<ReportDetailResponse>.Success(await MapDetailAsync(report, includeInternal: true, cancellationToken));
    }

    public async Task<ReportResult<ReportDetailResponse>> UpdateByAdminAsync(
        Guid adminUserId,
        Guid reportId,
        AdminUpdateReportRequest request,
        CancellationToken cancellationToken)
    {
        if (request.AssignToMe && request.Unassign)
        {
            return ReportResult<ReportDetailResponse>.Failure("validation_error", "No se puede asignar y desasignar el reporte a la vez.");
        }

        bool hasAction = request.Status.HasValue || request.Priority.HasValue || request.AssignToMe || request.Unassign ||
            !string.IsNullOrWhiteSpace(request.PublicReply) || !string.IsNullOrWhiteSpace(request.InternalNote);
        if (!hasAction)
        {
            return ReportResult<ReportDetailResponse>.Failure("validation_error", "Debes indicar al menos una acción.");
        }

        SupportReport? report = await _reportsDbContext.Reports
            .Include(item => item.Events)
            .FirstOrDefaultAsync(item => item.Id == reportId, cancellationToken);

        if (report is null)
        {
            return ReportResult<ReportDetailResponse>.Failure("not_found", "Reporte no encontrado.");
        }

        DateTime now = DateTime.UtcNow;

        if (request.Status.HasValue && request.Status.Value != report.Status)
        {
            if (!Enum.IsDefined(request.Status.Value))
            {
                return ReportResult<ReportDetailResponse>.Failure("validation_error", "El estado no es válido.");
            }

            report.Events.Add(StatusEvent(report, adminUserId, request.Status.Value, now));
            report.Status = request.Status.Value;
            if (report.Status is ReportStatus.Resolved or ReportStatus.Rejected)
            {
                report.ResolvedAtUtc = now;
                report.ResolvedByAdminUserId = adminUserId;
            }
            else
            {
                report.ResolvedAtUtc = null;
                report.ResolvedByAdminUserId = null;
            }
        }

        if (request.Priority.HasValue && request.Priority.Value != report.Priority)
        {
            if (!Enum.IsDefined(request.Priority.Value))
            {
                return ReportResult<ReportDetailResponse>.Failure("validation_error", "La prioridad no es válida.");
            }

            report.Events.Add(new ReportEvent
            {
                Id = Guid.NewGuid(), ActorUserId = adminUserId, Kind = ReportEventKind.PriorityChanged,
                IsInternal = true, PreviousPriority = report.Priority, NewPriority = request.Priority.Value, CreatedAtUtc = now
            });
            report.Priority = request.Priority.Value;
        }

        Guid? newAssignee = request.AssignToMe ? adminUserId : request.Unassign ? null : report.AssignedAdminUserId;
        if (newAssignee != report.AssignedAdminUserId)
        {
            report.Events.Add(new ReportEvent
            {
                Id = Guid.NewGuid(), ActorUserId = adminUserId, Kind = ReportEventKind.AssignmentChanged,
                IsInternal = true, PreviousAssignedAdminUserId = report.AssignedAdminUserId,
                NewAssignedAdminUserId = newAssignee, CreatedAtUtc = now
            });
            report.AssignedAdminUserId = newAssignee;
        }

        AddMessageEvent(report, adminUserId, request.PublicReply, ReportEventKind.AdminReply, isInternal: false, now);
        AddMessageEvent(report, adminUserId, request.InternalNote, ReportEventKind.InternalNote, isInternal: true, now);
        report.UpdatedAtUtc = now;

        await _reportsDbContext.SaveChangesAsync(cancellationToken);
        return ReportResult<ReportDetailResponse>.Success(await MapDetailAsync(report, includeInternal: true, cancellationToken));
    }

    private async Task<ReportDetailResponse> MapDetailAsync(
        SupportReport report,
        bool includeInternal,
        CancellationToken cancellationToken)
    {
        ReportDetailResponse response = new()
        {
            Id = report.Id,
            ReporterUserId = report.ReporterUserId,
            Category = report.Category,
            Status = report.Status,
            Priority = report.Priority,
            Subject = report.Subject,
            Description = report.Description,
            TicketId = report.TicketId,
            BusinessId = report.BusinessId,
            PromotionCampaignId = report.PromotionCampaignId,
            AssignedAdminUserId = report.AssignedAdminUserId,
            ResolvedByAdminUserId = report.ResolvedByAdminUserId,
            OccurredAtUtc = report.OccurredAtUtc,
            PageUrl = report.PageUrl,
            AppVersion = report.AppVersion,
            CreatedAtUtc = report.CreatedAtUtc,
            UpdatedAtUtc = report.UpdatedAtUtc,
            ResolvedAtUtc = report.ResolvedAtUtc,
            Timeline = report.Events
                .Where(reportEvent => includeInternal || !reportEvent.IsInternal)
                .OrderBy(reportEvent => reportEvent.CreatedAtUtc)
                .Select(MapEvent)
                .ToArray()
        };

        if (includeInternal)
        {
            Guid[] userIds = new[] { report.ReporterUserId, report.AssignedAdminUserId }
                .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
            Dictionary<Guid, UserAccount> users = await _usersDbContext.Users.AsNoTracking()
                .Where(user => userIds.Contains(user.Id))
                .ToDictionaryAsync(user => user.Id, cancellationToken);
            response.Reporter = users.TryGetValue(report.ReporterUserId, out UserAccount? reporter) ? MapUser(reporter) : null;
            response.AssignedAdmin = report.AssignedAdminUserId.HasValue && users.TryGetValue(report.AssignedAdminUserId.Value, out UserAccount? admin)
                ? MapUser(admin) : null;
        }

        if (report.TicketId.HasValue)
        {
            response.Ticket = await _fidelityDbContext.Tickets.AsNoTracking()
                .Where(ticket => ticket.Id == report.TicketId.Value)
                .Select(ticket => new ReportReferenceResponse
                {
                    Id = ticket.Id,
                    Label = ticket.TituloCanje ?? ticket.Nombre
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (report.BusinessId.HasValue)
        {
            response.Business = await _negociosDbContext.Negocios.AsNoTracking()
                .Where(business => business.Id == report.BusinessId.Value)
                .Select(business => new ReportReferenceResponse { Id = business.Id, Label = business.NombreComercial })
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (report.PromotionCampaignId.HasValue)
        {
            response.Promotion = await _promotionsDbContext.Campaigns.AsNoTracking()
                .Where(campaign => campaign.Id == report.PromotionCampaignId.Value)
                .Select(campaign => new ReportReferenceResponse { Id = campaign.Id, Label = campaign.TicketNombreSnapshot })
                .FirstOrDefaultAsync(cancellationToken);
        }

        return response;
    }

    private static IQueryable<SupportReport> ApplyFilters(
        IQueryable<SupportReport> query,
        ReportStatus? status,
        ReportPriority? priority,
        ReportCategory? category,
        Guid? assignedAdminUserId,
        bool? unassigned,
        string? search)
    {
        if (status.HasValue) query = query.Where(report => report.Status == status.Value);
        if (priority.HasValue) query = query.Where(report => report.Priority == priority.Value);
        if (category.HasValue) query = query.Where(report => report.Category == category.Value);
        if (assignedAdminUserId.HasValue) query = query.Where(report => report.AssignedAdminUserId == assignedAdminUserId.Value);
        if (unassigned == true) query = query.Where(report => report.AssignedAdminUserId == null);
        if (!string.IsNullOrWhiteSpace(search))
        {
            string value = search.Trim();
            query = query.Where(report => report.Subject.Contains(value) || report.Description.Contains(value));
        }

        return query;
    }

    private static async Task<PaginatedReportResponse<ReportSummaryResponse>> PaginateAsync(
        IQueryable<SupportReport> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        int totalItems = await query.CountAsync(cancellationToken);
        ReportSummaryResponse[] items = await query
            .OrderByDescending(report => report.UpdatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(report => new ReportSummaryResponse
            {
                Id = report.Id,
                ReporterUserId = report.ReporterUserId,
                Category = report.Category,
                Status = report.Status,
                Priority = report.Priority,
                Subject = report.Subject,
                TicketId = report.TicketId,
                BusinessId = report.BusinessId,
                PromotionCampaignId = report.PromotionCampaignId,
                AssignedAdminUserId = report.AssignedAdminUserId,
                CreatedAtUtc = report.CreatedAtUtc,
                UpdatedAtUtc = report.UpdatedAtUtc,
                ResolvedAtUtc = report.ResolvedAtUtc
            })
            .ToArrayAsync(cancellationToken);

        return new PaginatedReportResponse<ReportSummaryResponse>
        {
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize),
            Items = items
        };
    }

    private static ReportCategoryOptionResponse Category(
        ReportCategory value,
        string label,
        string description,
        bool ticket = false,
        bool business = false,
        bool promotion = false) => new()
        {
            Value = value,
            Label = label,
            Description = description,
            SupportsTicket = ticket,
            SupportsBusiness = business,
            SupportsPromotion = promotion
        };

    private static ReportEvent StatusEvent(SupportReport report, Guid actorUserId, ReportStatus newStatus, DateTime now) => new()
    {
        Id = Guid.NewGuid(), ActorUserId = actorUserId, Kind = ReportEventKind.StatusChanged,
        IsInternal = false, PreviousStatus = report.Status, NewStatus = newStatus, CreatedAtUtc = now
    };

    private static void AddMessageEvent(
        SupportReport report,
        Guid actorUserId,
        string? message,
        ReportEventKind kind,
        bool isInternal,
        DateTime now)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        report.Events.Add(new ReportEvent
        {
            Id = Guid.NewGuid(), ActorUserId = actorUserId, Kind = kind,
            IsInternal = isInternal, Message = message.Trim(), CreatedAtUtc = now
        });
    }

    private static ReportEventResponse MapEvent(ReportEvent reportEvent) => new()
    {
        Id = reportEvent.Id,
        ActorUserId = reportEvent.ActorUserId,
        Kind = reportEvent.Kind,
        IsInternal = reportEvent.IsInternal,
        Message = reportEvent.Message,
        PreviousStatus = reportEvent.PreviousStatus,
        NewStatus = reportEvent.NewStatus,
        PreviousPriority = reportEvent.PreviousPriority,
        NewPriority = reportEvent.NewPriority,
        PreviousAssignedAdminUserId = reportEvent.PreviousAssignedAdminUserId,
        NewAssignedAdminUserId = reportEvent.NewAssignedAdminUserId,
        CreatedAtUtc = reportEvent.CreatedAtUtc
    };

    private static ReportUserResponse MapUser(UserAccount user) => new()
    {
        Id = user.Id,
        UserName = user.UserName,
        DisplayName = user.DisplayName,
        Email = user.Email
    };

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
