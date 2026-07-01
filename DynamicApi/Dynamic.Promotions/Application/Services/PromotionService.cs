using System.Text.Json;
using System.Text.Json.Serialization;
using Dynamic.Fidelity.Application.Contracts.Services;
using Dynamic.Fidelity.Application.DTOs.Requests;
using Dynamic.Fidelity.Application.DTOs.Responses;
using Dynamic.Fidelity.Application.Mappings;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Domain.Enums;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Dynamic.Negocios.Domain.Enums;
using Dynamic.Negocios.Infrastructure.Persistence;
using Dynamic.Promotions.Application.Common;
using Dynamic.Promotions.Application.Contracts;
using Dynamic.Promotions.Application.DTOs.Requests;
using Dynamic.Promotions.Application.DTOs.Responses;
using Dynamic.Promotions.Domain.Entities;
using Dynamic.Promotions.Domain.Enums;
using Dynamic.Promotions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dynamic.Promotions.Application.Services;

public class PromotionService : IPromotionService
{
    public const string BuildAudienceMessageType = "BuildAudience";
    private const int MaxPageSize = 100;
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly DynamicPromotionsDbContext _promotionsDbContext;
    private readonly DynamicNegociosDbContext _negociosDbContext;
    private readonly DynamicFidelityDbContext _fidelityDbContext;
    private readonly ITicketService _ticketService;

    public PromotionService(
        DynamicPromotionsDbContext promotionsDbContext,
        DynamicNegociosDbContext negociosDbContext,
        DynamicFidelityDbContext fidelityDbContext,
        ITicketService ticketService)
    {
        _promotionsDbContext = promotionsDbContext;
        _negociosDbContext = negociosDbContext;
        _fidelityDbContext = fidelityDbContext;
        _ticketService = ticketService;
    }

    public async Task<PromotionServiceResult<PromotionCampaignResponse>> CreateCampaignAsync(
        Guid negocioId,
        Guid requesterUserId,
        bool requesterIsAdmin,
        CreatePromotionCampaignRequest request,
        CancellationToken cancellationToken = default)
    {
        DateTime now = DateTime.UtcNow;
        DateTime scheduledAt = request.ScheduledAtUtc ?? now;
        DateTime startsAt = request.StartsAtUtc ?? scheduledAt;

        string? validationError = ValidateRequest(request, now, scheduledAt, startsAt);
        if (validationError is not null)
        {
            return PromotionServiceResult<PromotionCampaignResponse>.Failure("validation_error", validationError);
        }

        var negocio = await _negociosDbContext.Negocios
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == negocioId && !item.IsDeleted, cancellationToken);

        if (negocio is null)
        {
            return PromotionServiceResult<PromotionCampaignResponse>.Failure("not_found", "Negocio no encontrado.");
        }

        bool isOwner = negocio.OwnerUserId == requesterUserId || await _negociosDbContext.NegociosUsuariosVinculaciones
            .AsNoTracking()
            .AnyAsync(link =>
                link.NegocioId == negocioId &&
                link.UserId == requesterUserId &&
                link.Activa &&
                !link.RevokedAtUtc.HasValue &&
                link.TipoVinculacion == TipoVinculacionNegocioUsuario.Propietario,
                cancellationToken);

        if (!requesterIsAdmin && !isOwner)
        {
            return PromotionServiceResult<PromotionCampaignResponse>.Failure(
                "forbidden",
                "Solo el propietario del negocio puede enviar promociones.");
        }

        string? idempotencyKey = Normalize(request.IdempotencyKey);
        if (idempotencyKey is not null)
        {
            PromotionCampaign? existing = await _promotionsDbContext.Campaigns
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    campaign => campaign.NegocioId == negocioId && campaign.IdempotencyKey == idempotencyKey,
                    cancellationToken);

            if (existing is not null)
            {
                return PromotionServiceResult<PromotionCampaignResponse>.Success(ToResponse(existing));
            }
        }

        PromotionServiceResult<Ticket> ticketResolutionResult = await ResolveTicketTemplateAsync(
            negocioId,
            requesterUserId,
            requesterIsAdmin,
            request,
            cancellationToken);
        if (!ticketResolutionResult.Succeeded || ticketResolutionResult.Data is null)
        {
            return PromotionServiceResult<PromotionCampaignResponse>.Failure(
                ticketResolutionResult.ErrorCode ?? "validation_error",
                ticketResolutionResult.ErrorMessage ?? "No se ha podido resolver el ticket de la campaña.");
        }

        Ticket ticketTemplate = ticketResolutionResult.Data;
        TicketResponse ticketSnapshot = ticketTemplate.ToResponse();

        PromotionCampaign campaign = new()
        {
            Id = Guid.NewGuid(),
            NegocioId = negocioId,
            CreatedByUserId = requesterUserId,
            TicketTemplateId = ticketTemplate.Id,
            NegocioNombreSnapshot = negocio.NombreComercial,
            NegocioSlugSnapshot = negocio.SlugPortal,
            NegocioLogoUrlSnapshot = negocio.LogoPrincipalUrl ?? negocio.IconoUrl,
            TicketNombreSnapshot = ticketTemplate.Nombre,
            TicketDescripcionSnapshot = ticketTemplate.Descripcion,
            TicketSnapshotJson = JsonSerializer.Serialize(ticketSnapshot, JsonOptions),
            FiltersJson = JsonSerializer.Serialize(request.Filters ?? new PromotionAudienceFiltersRequest(), JsonOptions),
            Status = PromotionCampaignStatus.Queued,
            PushEnabled = negocio.PermiteNotificacionesPush,
            IdempotencyKey = idempotencyKey,
            StartsAtUtc = startsAt,
            ExpiresAtUtc = request.ExpiresAtUtc,
            ScheduledAtUtc = scheduledAt,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        PromotionOutboxMessage outboxMessage = new()
        {
            Id = Guid.NewGuid(),
            AggregateId = campaign.Id,
            Type = BuildAudienceMessageType,
            PayloadJson = JsonSerializer.Serialize(new { campaignId = campaign.Id }, JsonOptions),
            Status = PromotionOutboxStatus.Pending,
            AvailableAtUtc = scheduledAt,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await using var transaction = await _promotionsDbContext.Database.BeginTransactionAsync(cancellationToken);
        await _promotionsDbContext.Campaigns.AddAsync(campaign, cancellationToken);
        await _promotionsDbContext.OutboxMessages.AddAsync(outboxMessage, cancellationToken);
        await _promotionsDbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return PromotionServiceResult<PromotionCampaignResponse>.Success(ToResponse(campaign));
    }

    public async Task<PromotionServiceResult<PromotionCampaignResponse>> GetCampaignAsync(
        Guid negocioId,
        Guid campaignId,
        Guid requesterUserId,
        bool requesterIsAdmin,
        CancellationToken cancellationToken = default)
    {
        bool isOwner = await _negociosDbContext.Negocios
            .AsNoTracking()
            .AnyAsync(item => item.Id == negocioId && !item.IsDeleted && item.OwnerUserId == requesterUserId, cancellationToken) ||
            await _negociosDbContext.NegociosUsuariosVinculaciones
                .AsNoTracking()
                .AnyAsync(link =>
                    link.NegocioId == negocioId &&
                    link.UserId == requesterUserId &&
                    link.Activa &&
                    !link.RevokedAtUtc.HasValue &&
                    link.TipoVinculacion == TipoVinculacionNegocioUsuario.Propietario,
                    cancellationToken);

        if (!requesterIsAdmin && !isOwner)
        {
            return PromotionServiceResult<PromotionCampaignResponse>.Failure("forbidden", "No puedes consultar esta campaña.");
        }

        PromotionCampaign? campaign = await _promotionsDbContext.Campaigns
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == campaignId && item.NegocioId == negocioId, cancellationToken);

        return campaign is null
            ? PromotionServiceResult<PromotionCampaignResponse>.Failure("not_found", "Campaña no encontrada.")
            : PromotionServiceResult<PromotionCampaignResponse>.Success(ToResponse(campaign));
    }

    public async Task<ReceivedPromotionsPageResponse> GetReceivedPromotionsAsync(
        Guid userId,
        int page,
        int pageSize,
        bool includeRead,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        DateTime now = DateTime.UtcNow;

        IQueryable<PromotionRecipient> query = _promotionsDbContext.Recipients
            .AsNoTracking()
            .Include(recipient => recipient.Campaign)
            .Where(recipient =>
                recipient.UserId == userId &&
                recipient.Status != PromotionRecipientStatus.Dismissed &&
                recipient.ExpiresAtUtc > now &&
                recipient.Campaign.Status == PromotionCampaignStatus.Sent &&
                recipient.Campaign.StartsAtUtc <= now);

        if (!includeRead)
        {
            query = query.Where(recipient => recipient.Status == PromotionRecipientStatus.Received);
        }

        int totalItems = await query.CountAsync(cancellationToken);
        int unreadCount = await _promotionsDbContext.Recipients
            .AsNoTracking()
            .CountAsync(recipient =>
                recipient.UserId == userId &&
                recipient.Status == PromotionRecipientStatus.Received &&
                recipient.ExpiresAtUtc > now &&
                recipient.Campaign.Status == PromotionCampaignStatus.Sent &&
                recipient.Campaign.StartsAtUtc <= now,
                cancellationToken);

        PromotionRecipient[] recipients = await query
            .OrderByDescending(recipient => recipient.ReceivedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        Guid[] recipientIds = recipients.Select(recipient => recipient.Id).ToArray();
        Dictionary<Guid, Guid> assignedTicketIdsByRecipient = recipientIds.Length == 0
            ? []
            : await _fidelityDbContext.Tickets
                .AsNoTracking()
                .Where(ticket => ticket.SourcePromotionRecipientId.HasValue && recipientIds.Contains(ticket.SourcePromotionRecipientId.Value))
                .ToDictionaryAsync(ticket => ticket.SourcePromotionRecipientId!.Value, ticket => ticket.Id, cancellationToken);

        return new ReceivedPromotionsPageResponse
        {
            Items = recipients.Select(recipient => ToReceivedResponse(recipient, assignedTicketIdsByRecipient)).ToArray(),
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize),
            UnreadCount = unreadCount
        };
    }

    public async Task<bool> MarkAsReadAsync(Guid userId, Guid recipientId, CancellationToken cancellationToken = default)
    {
        PromotionRecipient? recipient = await _promotionsDbContext.Recipients
            .FirstOrDefaultAsync(item => item.Id == recipientId && item.UserId == userId, cancellationToken);

        if (recipient is null)
        {
            return false;
        }

        if (recipient.Status == PromotionRecipientStatus.Received)
        {
            DateTime now = DateTime.UtcNow;
            recipient.Status = PromotionRecipientStatus.Read;
            recipient.ReadAtUtc = now;
            recipient.UpdatedAtUtc = now;
            await _promotionsDbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    private static string? ValidateRequest(
        CreatePromotionCampaignRequest request,
        DateTime now,
        DateTime scheduledAt,
        DateTime startsAt)
    {
        bool hasExistingTicket = request.TicketTemplateId.HasValue;
        bool hasTicketToCreate = request.Ticket is not null;

        if (hasExistingTicket == hasTicketToCreate)
        {
            return "Debes indicar un ticket existente o un ticket nuevo para la campaña.";
        }

        if (request.ExpiresAtUtc <= now || request.ExpiresAtUtc <= startsAt || request.ExpiresAtUtc <= scheduledAt)
        {
            return "La fecha de expiracion debe ser futura y posterior al inicio y al envio.";
        }

        PromotionAudienceFiltersRequest filters = request.Filters ?? new();
        if (filters.MinimumAge is < 0 or > 130 || filters.MaximumAge is < 0 or > 130 ||
            filters.MinimumAge.HasValue && filters.MaximumAge.HasValue && filters.MinimumAge > filters.MaximumAge)
        {
            return "El rango de edad no es valido.";
        }

        if (filters.MinimumDaysSinceLastPointsEarned is < 0 || filters.MaximumDaysSinceLastPointsEarned is < 0)
        {
            return "Los dias desde la ultima acumulacion no pueden ser negativos.";
        }

        if (filters.MinimumDaysSinceLastPointsEarned.HasValue &&
            filters.MaximumDaysSinceLastPointsEarned.HasValue &&
            filters.MinimumDaysSinceLastPointsEarned > filters.MaximumDaysSinceLastPointsEarned)
        {
            return "El rango de dias desde la ultima acumulacion no es valido.";
        }

        if (filters.MinimumCurrentPoints.HasValue && filters.MaximumCurrentPoints.HasValue &&
            filters.MinimumCurrentPoints > filters.MaximumCurrentPoints ||
            filters.MinimumTotalPointsEarned.HasValue && filters.MaximumTotalPointsEarned.HasValue &&
            filters.MinimumTotalPointsEarned > filters.MaximumTotalPointsEarned ||
            filters.MinimumTotalPointsSpent.HasValue && filters.MaximumTotalPointsSpent.HasValue &&
            filters.MinimumTotalPointsSpent > filters.MaximumTotalPointsSpent)
        {
            return "Los rangos de puntos no son validos.";
        }

        if (filters.LastPointsEarnedAfterUtc.HasValue && filters.LastPointsEarnedBeforeUtc.HasValue &&
            filters.LastPointsEarnedAfterUtc > filters.LastPointsEarnedBeforeUtc ||
            filters.LastActivityAfterUtc.HasValue && filters.LastActivityBeforeUtc.HasValue &&
            filters.LastActivityAfterUtc > filters.LastActivityBeforeUtc ||
            filters.CustomerSinceAfterUtc.HasValue && filters.CustomerSinceBeforeUtc.HasValue &&
            filters.CustomerSinceAfterUtc > filters.CustomerSinceBeforeUtc ||
            filters.LastPointsSpentAfterUtc.HasValue && filters.LastPointsSpentBeforeUtc.HasValue &&
            filters.LastPointsSpentAfterUtc > filters.LastPointsSpentBeforeUtc ||
            filters.RegisteredAfterUtc.HasValue && filters.RegisteredBeforeUtc.HasValue &&
            filters.RegisteredAfterUtc > filters.RegisteredBeforeUtc ||
            filters.LastAppSeenAfterUtc.HasValue && filters.LastAppSeenBeforeUtc.HasValue &&
            filters.LastAppSeenAfterUtc > filters.LastAppSeenBeforeUtc)
        {
            return "Uno de los rangos de fechas no es valido.";
        }

        if (filters.MinimumDaysSinceLastAppSeen is < 0 || filters.MaximumDaysSinceLastAppSeen is < 0 ||
            filters.MinimumDaysSinceLastAppSeen.HasValue && filters.MaximumDaysSinceLastAppSeen.HasValue &&
            filters.MinimumDaysSinceLastAppSeen > filters.MaximumDaysSinceLastAppSeen)
        {
            return "El rango de dias desde la ultima actividad en la app no es valido.";
        }

        if (filters.BirthMonth is < 1 or > 12)
        {
            return "El mes de nacimiento debe estar entre 1 y 12.";
        }

        if (filters.MinimumTicketCount is < 0 || filters.MaximumTicketCount is < 0 ||
            filters.MinimumUsedTicketCount is < 0 || filters.MaximumUsedTicketCount is < 0 ||
            filters.MinimumTicketCount.HasValue && filters.MaximumTicketCount.HasValue &&
            filters.MinimumTicketCount > filters.MaximumTicketCount ||
            filters.MinimumUsedTicketCount.HasValue && filters.MaximumUsedTicketCount.HasValue &&
            filters.MinimumUsedTicketCount > filters.MaximumUsedTicketCount)
        {
            return "Los rangos de tickets no son validos.";
        }

        if (filters.PostalCodes?.Count > 100 || filters.Regions?.Count > 100 ||
            filters.CountryCodes?.Count > 100 || filters.Languages?.Count > 100)
        {
            return "Cada filtro geografico o de idioma admite un maximo de 100 valores.";
        }

        return null;
    }

    private static PromotionCampaignResponse ToResponse(PromotionCampaign campaign)
        => new()
        {
            Id = campaign.Id,
            NegocioId = campaign.NegocioId,
            NegocioName = campaign.NegocioNombreSnapshot,
            TicketTemplateId = campaign.TicketTemplateId,
            Ticket = JsonSerializer.Deserialize<TicketResponse>(campaign.TicketSnapshotJson, JsonOptions) ?? new TicketResponse
            {
                Id = campaign.TicketTemplateId,
                NegocioId = campaign.NegocioId,
                Nombre = campaign.TicketNombreSnapshot,
                Descripcion = campaign.TicketDescripcionSnapshot
            },
            Status = campaign.Status,
            AudienceCount = campaign.AudienceCount,
            PushEligibleCount = campaign.PushEligibleCount,
            PushDeliveredCount = campaign.PushDeliveredCount,
            PushFailedCount = campaign.PushFailedCount,
            PushEnabled = campaign.PushEnabled,
            Filters = JsonSerializer.Deserialize<PromotionAudienceFiltersRequest>(campaign.FiltersJson, JsonOptions) ?? new(),
            StartsAtUtc = campaign.StartsAtUtc,
            ExpiresAtUtc = campaign.ExpiresAtUtc,
            ScheduledAtUtc = campaign.ScheduledAtUtc,
            CreatedAtUtc = campaign.CreatedAtUtc,
            AudienceProcessedAtUtc = campaign.AudienceProcessedAtUtc,
            LastError = campaign.LastError
        };

    private static ReceivedPromotionResponse ToReceivedResponse(
        PromotionRecipient recipient,
        IReadOnlyDictionary<Guid, Guid> assignedTicketIdsByRecipient)
        => new()
        {
            Id = recipient.Id,
            CampaignId = recipient.CampaignId,
            TicketTemplateId = recipient.Campaign.TicketTemplateId,
            AssignedTicketId = assignedTicketIdsByRecipient.TryGetValue(recipient.Id, out Guid assignedTicketId)
                ? assignedTicketId
                : null,
            Negocio = new PromotionBusinessSummaryResponse
            {
                Id = recipient.Campaign.NegocioId,
                Name = recipient.Campaign.NegocioNombreSnapshot,
                Slug = recipient.Campaign.NegocioSlugSnapshot,
                LogoUrl = recipient.Campaign.NegocioLogoUrlSnapshot
            },
            Ticket = JsonSerializer.Deserialize<TicketResponse>(recipient.Campaign.TicketSnapshotJson, JsonOptions) ?? new TicketResponse
            {
                Id = recipient.Campaign.TicketTemplateId,
                NegocioId = recipient.Campaign.NegocioId,
                Nombre = recipient.Campaign.TicketNombreSnapshot,
                Descripcion = recipient.Campaign.TicketDescripcionSnapshot
            },
            StartsAtUtc = recipient.Campaign.StartsAtUtc,
            ExpiresAtUtc = recipient.ExpiresAtUtc,
            ReceivedAtUtc = recipient.ReceivedAtUtc,
            IsRead = recipient.Status == PromotionRecipientStatus.Read,
            ReadAtUtc = recipient.ReadAtUtc
        };

    private static JsonSerializerOptions CreateJsonOptions()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<PromotionServiceResult<Ticket>> ResolveTicketTemplateAsync(
        Guid negocioId,
        Guid requesterUserId,
        bool requesterIsAdmin,
        CreatePromotionCampaignRequest request,
        CancellationToken cancellationToken)
    {
        if (request.TicketTemplateId.HasValue)
        {
            Ticket? existingTicket = await _fidelityDbContext.Tickets
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    ticket => ticket.Id == request.TicketTemplateId.Value &&
                              ticket.NegocioId == negocioId &&
                              ticket.EsPlantilla &&
                              !ticket.UserId.HasValue,
                    cancellationToken);

            if (existingTicket is null)
            {
                return PromotionServiceResult<Ticket>.Failure("not_found", "El ticket seleccionado no existe o no pertenece al negocio.");
            }

            if (existingTicket.CategoriaEnvioEspecial != CategoriaEnvioTicket.General)
            {
                return PromotionServiceResult<Ticket>.Failure("validation_error", "Solo puedes enviar tickets generales desde campañas.");
            }

            return PromotionServiceResult<Ticket>.Success(existingTicket);
        }

        if (request.Ticket is null)
        {
            return PromotionServiceResult<Ticket>.Failure("validation_error", "Debes indicar un ticket para crear la campaña.");
        }

        CreateTicketRequest createTicketRequest = request.Ticket;
        createTicketRequest.CategoriaEnvioEspecial = CategoriaEnvioTicket.General;

        var createResult = await _ticketService.CreateAsync(
            negocioId,
            requesterUserId,
            requesterIsAdmin,
            createTicketRequest,
            cancellationToken);

        if (!createResult.Succeeded || createResult.Data is null)
        {
            return PromotionServiceResult<Ticket>.Failure(
                createResult.ErrorCode ?? "validation_error",
                createResult.ErrorMessage ?? "No se ha podido crear el ticket de la campaña.");
        }

        Ticket? createdTicket = await _fidelityDbContext.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(ticket => ticket.Id == createResult.Data.Id, cancellationToken);

        return createdTicket is null
            ? PromotionServiceResult<Ticket>.Failure("not_found", "El ticket creado para la campaña no se ha podido recuperar.")
            : PromotionServiceResult<Ticket>.Success(createdTicket);
    }
}
