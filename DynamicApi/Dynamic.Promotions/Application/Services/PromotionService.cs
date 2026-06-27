using System.Text.Json;
using System.Text.Json.Serialization;
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

    public PromotionService(
        DynamicPromotionsDbContext promotionsDbContext,
        DynamicNegociosDbContext negociosDbContext)
    {
        _promotionsDbContext = promotionsDbContext;
        _negociosDbContext = negociosDbContext;
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

        PromotionCampaign campaign = new()
        {
            Id = Guid.NewGuid(),
            NegocioId = negocioId,
            CreatedByUserId = requesterUserId,
            NegocioNombreSnapshot = negocio.NombreComercial,
            NegocioSlugSnapshot = negocio.SlugPortal,
            NegocioLogoUrlSnapshot = negocio.LogoPrincipalUrl ?? negocio.IconoUrl,
            Title = request.Title.Trim(),
            Message = request.Message.Trim(),
            ImageUrl = Normalize(request.ImageUrl),
            ActionLabel = Normalize(request.ActionLabel),
            DeepLink = Normalize(request.DeepLink),
            Conditions = Normalize(request.Conditions),
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

        return new ReceivedPromotionsPageResponse
        {
            Items = recipients.Select(ToReceivedResponse).ToArray(),
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
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Message))
        {
            return "Titulo y mensaje son obligatorios.";
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

        if (filters.Cities?.Count > 100 || filters.Regions?.Count > 100 ||
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
            Title = campaign.Title,
            Message = campaign.Message,
            ImageUrl = campaign.ImageUrl,
            ActionLabel = campaign.ActionLabel,
            DeepLink = campaign.DeepLink,
            Conditions = campaign.Conditions,
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

    private static ReceivedPromotionResponse ToReceivedResponse(PromotionRecipient recipient)
        => new()
        {
            Id = recipient.Id,
            CampaignId = recipient.CampaignId,
            Negocio = new PromotionBusinessSummaryResponse
            {
                Id = recipient.Campaign.NegocioId,
                Name = recipient.Campaign.NegocioNombreSnapshot,
                Slug = recipient.Campaign.NegocioSlugSnapshot,
                LogoUrl = recipient.Campaign.NegocioLogoUrlSnapshot
            },
            Title = recipient.Campaign.Title,
            Message = recipient.Campaign.Message,
            ImageUrl = recipient.Campaign.ImageUrl,
            ActionLabel = recipient.Campaign.ActionLabel,
            DeepLink = recipient.Campaign.DeepLink,
            Conditions = recipient.Campaign.Conditions,
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
}
