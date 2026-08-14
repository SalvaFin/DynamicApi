using System.Data.Common;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dynamic.Promotions.Application.Contracts;
using Dynamic.Promotions.Application.DTOs.Requests;
using Dynamic.Promotions.Application.DTOs.Responses;
using Dynamic.Promotions.Application.Options;
using Dynamic.Promotions.Domain.Entities;
using Dynamic.Promotions.Domain.Enums;
using Dynamic.Promotions.Infrastructure.Persistence;
using Dynamic.Fidelity.Application.Contracts.Services;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Domain.Enums;
using Dynamic.Notify.Application.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace Dynamic.Promotions.Application.Services;

public class PromotionAudienceBuilder : IPromotionAudienceBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly DynamicPromotionsDbContext _dbContext;
    private readonly FirebasePushOptions _firebaseOptions;
    private readonly SmtpOptions _smtpOptions;
    private readonly ITicketEventPublisher _ticketEventPublisher;
    private readonly ILogger<PromotionAudienceBuilder> _logger;

    public PromotionAudienceBuilder(
        DynamicPromotionsDbContext dbContext,
        IOptions<FirebasePushOptions> firebaseOptions,
        IOptions<SmtpOptions> smtpOptions,
        ITicketEventPublisher ticketEventPublisher,
        ILogger<PromotionAudienceBuilder> logger)
    {
        _dbContext = dbContext;
        _firebaseOptions = firebaseOptions.Value;
        _smtpOptions = smtpOptions.Value;
        _ticketEventPublisher = ticketEventPublisher;
        _logger = logger;
    }

    public async Task BuildAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        PromotionCampaign? campaign = await _dbContext.Campaigns
            .FirstOrDefaultAsync(item => item.Id == campaignId, cancellationToken);

        if (campaign is null || campaign.Status is PromotionCampaignStatus.Sent or PromotionCampaignStatus.Cancelled)
        {
            return;
        }

        DateTime now = DateTime.UtcNow;
        PromotionAudienceFiltersRequest filters = JsonSerializer.Deserialize<PromotionAudienceFiltersRequest>(
            campaign.FiltersJson,
            JsonOptions) ?? new();

        campaign.Status = PromotionCampaignStatus.ProcessingAudience;
        campaign.UpdatedAtUtc = now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await InsertRecipientsAsync(campaign, filters, now, cancellationToken);
            await InsertAssignedTicketsAsync(campaign.Id, now, cancellationToken);
            if (campaign.PushEnabled && _firebaseOptions.Enabled)
            {
                await InsertPushDeliveriesAsync(campaign.Id, campaign.StartsAtUtc, now, cancellationToken);
            }
            if (campaign.EmailEnabled)
            {
                await InsertEmailDeliveriesAsync(campaign.Id, campaign.StartsAtUtc, now, cancellationToken);
            }

            campaign.AudienceCount = await _dbContext.Recipients
                .CountAsync(recipient => recipient.CampaignId == campaign.Id, cancellationToken);
            campaign.PushEligibleCount = await _dbContext.Deliveries
                .Select(delivery => new { delivery.CampaignId, delivery.RecipientId })
                .Where(delivery => delivery.CampaignId == campaign.Id)
                .Select(delivery => delivery.RecipientId)
                .Distinct()
                .CountAsync(cancellationToken);
            campaign.EmailEligibleCount = await _dbContext.EmailDeliveries
                .CountAsync(delivery => delivery.CampaignId == campaign.Id, cancellationToken);
            campaign.Status = PromotionCampaignStatus.Sent;
            campaign.AudienceProcessedAtUtc = now;
            campaign.UpdatedAtUtc = now;
            campaign.LastError = null;

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            try
            {
                IReadOnlyCollection<Ticket> assignedTickets = await LoadAssignedTicketsAsync(campaign.Id, cancellationToken);
                foreach (Ticket assignedTicket in assignedTickets)
                {
                    await _ticketEventPublisher.PublishReceivedAsync(assignedTicket, "promotion", cancellationToken);
                }
            }
            catch (Exception notificationException)
            {
                // La campaña y sus tickets ya están confirmados. Un fallo del canal efímero
                // no debe revertir ni marcar como fallida la operación de negocio.
                _logger.LogWarning(
                    notificationException,
                    "No se pudieron publicar todos los tickets de la campaña {CampaignId} por SignalR.",
                    campaign.Id);
            }
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            campaign.Status = PromotionCampaignStatus.Failed;
            campaign.LastError = Truncate(ex.Message, 2000);
            campaign.UpdatedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    public async Task<PromotionAudiencePreviewResponse> PreviewAsync(
        Guid negocioId,
        PromotionAudienceFiltersRequest filters,
        bool businessPushEnabled,
        bool businessEmailEnabled,
        CancellationToken cancellationToken = default)
    {
        DateTime now = DateTime.UtcNow;
        AudienceSql audienceSql = BuildAudienceSql(negocioId, filters, now);

        long audienceCount = await ExecuteScalarLongAsync(
            $"SELECT COUNT(*) {audienceSql.FromWhereSql}",
            audienceSql.Parameters,
            cancellationToken);

        long pushEligibleCount = 0;
        bool pushAvailable = businessPushEnabled && _firebaseOptions.Enabled;
        if (pushAvailable)
        {
            pushEligibleCount = await ExecuteScalarLongAsync(
                $"""
                SELECT COUNT(*)
                {audienceSql.FromWhereSql}
                  AND EXISTS (
                      SELECT 1
                      FROM `user_devices` preview_device
                      WHERE preview_device.`UserId` = candidates.`UserId`
                        AND preview_device.`NotificationsEnabled` = 1
                        AND preview_device.`PushToken` IS NOT NULL
                        AND preview_device.`PushToken` <> ''
                        AND preview_device.`PushProvider` = 'Firebase'
                  )
                """,
                audienceSql.Parameters,
                cancellationToken);
        }

        bool emailAvailable = businessEmailEnabled && _smtpOptions.Enabled;
        long emailEligibleCount = 0;
        if (businessEmailEnabled)
        {
            emailEligibleCount = await ExecuteScalarLongAsync(
                $"SELECT COUNT(*) {audienceSql.FromWhereSql} AND candidates.`PermiteCorreosPromocionales` = 1 AND user_account.`Email` IS NOT NULL AND user_account.`Email` <> ''",
                audienceSql.Parameters,
                cancellationToken);
        }

        return new PromotionAudiencePreviewResponse
        {
            NegocioId = negocioId,
            AudienceCount = ToInt32Count(audienceCount),
            PushEligibleCount = ToInt32Count(pushEligibleCount),
            BusinessPushEnabled = businessPushEnabled,
            FirebasePushEnabled = _firebaseOptions.Enabled,
            PushAvailable = pushAvailable,
            EmailEligibleCount = ToInt32Count(emailEligibleCount),
            BusinessEmailEnabled = businessEmailEnabled,
            SmtpEmailEnabled = _smtpOptions.Enabled,
            EmailAvailable = emailAvailable,
            CalculatedAtUtc = now,
            Filters = filters
        };
    }

    private async Task InsertRecipientsAsync(
        PromotionCampaign campaign,
        PromotionAudienceFiltersRequest filters,
        DateTime now,
        CancellationToken cancellationToken)
    {
        AudienceSql audienceSql = BuildAudienceSql(campaign.NegocioId, filters, now);
        List<(string Name, object? Value)> parameters =
        [
            ("@campaignId", campaign.Id.ToString()),
            ("@expiresAt", campaign.ExpiresAtUtc),
            .. audienceSql.Parameters
        ];

        string sql = $"""
            INSERT IGNORE INTO `promotion_recipients`
                (`Id`, `CampaignId`, `UserId`, `Status`, `ReceivedAtUtc`, `ExpiresAtUtc`, `CreatedAtUtc`, `UpdatedAtUtc`)
            SELECT UUID(), @campaignId, candidates.`UserId`, 'Received', @now, @expiresAt, @now, @now
            {audienceSql.FromWhereSql}
            """;

        await ExecuteCommandAsync(sql, parameters, cancellationToken);
    }

    private AudienceSql BuildAudienceSql(
        Guid negocioId,
        PromotionAudienceFiltersRequest filters,
        DateTime now)
    {
        StringBuilder where = new();
        List<(string Name, object? Value)> parameters =
        [
            ("@negocioId", negocioId.ToString()),
            ("@now", now)
        ];

        AppendFilters(where, parameters, filters, now);

        string fromWhereSql = $"""
            FROM (
                SELECT audience_source.`UserId`,
                       audience_source.`FechaAltaUtc`,
                       audience_source.`UltimaActividadUtc`,
                       audience_source.`PermiteCorreosPromocionales`
                FROM `negocio_audience_memberships` audience_source
                WHERE audience_source.`NegocioId` = @negocioId
                  AND audience_source.`Activa` = 1
                  AND audience_source.`FechaBajaUtc` IS NULL
            ) candidates
            INNER JOIN `users` user_account ON user_account.`Id` = candidates.`UserId`
            LEFT JOIN `fidelity_points` points_data
                ON points_data.`NegocioId` = @negocioId AND points_data.`UserId` = candidates.`UserId`
            LEFT JOIN (
                SELECT ticket_stats_source.`UserId`,
                       MIN(ticket_stats_source.`CreatedAtUtc`) AS `FirstTicketAtUtc`,
                       MAX(ticket_stats_source.`UpdatedAtUtc`) AS `LastTicketAtUtc`,
                       COUNT(*) AS `TotalTickets`,
                       SUM(CASE WHEN ticket_stats_source.`Usado` = 1 THEN 1 ELSE 0 END) AS `UsedTickets`,
                       MAX(CASE
                           WHEN ticket_stats_source.`Activo` = 1
                            AND ticket_stats_source.`Usado` = 0
                            AND ticket_stats_source.`ExpiresAtUtc` > @now THEN 1
                           ELSE 0
                       END) AS `HasActiveTickets`
                FROM `fidelity_tickets` ticket_stats_source
                WHERE ticket_stats_source.`NegocioId` = @negocioId
                  AND ticket_stats_source.`UserId` IS NOT NULL
                GROUP BY ticket_stats_source.`UserId`
            ) ticket_stats ON ticket_stats.`UserId` = candidates.`UserId`
            WHERE user_account.`RegistrationCompleted` = 1
              AND user_account.`Status` = 'Active'
              {where}
            """;

        return new AudienceSql(fromWhereSql, parameters);
    }

    private async Task InsertPushDeliveriesAsync(
        Guid campaignId,
        DateTime startsAtUtc,
        DateTime now,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT IGNORE INTO `promotion_deliveries`
                (`Id`, `CampaignId`, `RecipientId`, `UserId`, `UserDeviceId`, `Provider`, `Status`,
                 `AttemptCount`, `NextAttemptAtUtc`, `CreatedAtUtc`, `UpdatedAtUtc`)
            SELECT UUID(), recipient.`CampaignId`, recipient.`Id`, recipient.`UserId`, device.`Id`,
                   device.`PushProvider`, 'Pending', 0, @nextAttemptAt, @now, @now
            FROM `promotion_recipients` recipient
            INNER JOIN `user_devices` device ON device.`UserId` = recipient.`UserId`
            WHERE recipient.`CampaignId` = @campaignId
              AND device.`NotificationsEnabled` = 1
              AND device.`PushToken` IS NOT NULL
              AND device.`PushToken` <> ''
              AND device.`PushProvider` = 'Firebase'
            """;

        await ExecuteCommandAsync(
            sql,
            [("@campaignId", campaignId.ToString()), ("@now", now), ("@nextAttemptAt", startsAtUtc > now ? startsAtUtc : now)],
            cancellationToken);
    }

    private async Task InsertEmailDeliveriesAsync(
        Guid campaignId,
        DateTime startsAtUtc,
        DateTime now,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT IGNORE INTO `promotion_email_deliveries`
                (`Id`, `CampaignId`, `RecipientId`, `UserId`, `Email`, `RecipientName`, `UnsubscribeToken`,
                 `Status`, `AttemptCount`, `NextAttemptAtUtc`, `CreatedAtUtc`, `UpdatedAtUtc`)
            SELECT UUID(), recipient.`CampaignId`, recipient.`Id`, recipient.`UserId`, user_account.`Email`,
                   COALESCE(NULLIF(user_account.`DisplayName`, ''), NULLIF(user_account.`FirstName`, ''), user_account.`UserName`),
                   UUID(), 'Pending', 0, @nextAttemptAt, @now, @now
            FROM `promotion_recipients` recipient
            INNER JOIN `promotion_campaigns` campaign ON campaign.`Id` = recipient.`CampaignId`
            INNER JOIN `users` user_account ON user_account.`Id` = recipient.`UserId`
            INNER JOIN `negocio_audience_memberships` audience
                ON audience.`NegocioId` = campaign.`NegocioId`
               AND audience.`UserId` = recipient.`UserId`
            WHERE recipient.`CampaignId` = @campaignId
              AND audience.`Activa` = 1
              AND audience.`FechaBajaUtc` IS NULL
              AND audience.`PermiteCorreosPromocionales` = 1
              AND user_account.`Email` IS NOT NULL
              AND user_account.`Email` <> ''
            """;

        await ExecuteCommandAsync(
            sql,
            [("@campaignId", campaignId.ToString()), ("@now", now), ("@nextAttemptAt", startsAtUtc > now ? startsAtUtc : now)],
            cancellationToken);
    }

    private async Task InsertAssignedTicketsAsync(
        Guid campaignId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT IGNORE INTO `fidelity_tickets`
                (`Id`, `NegocioId`, `UserId`, `ParentTicketId`, `SourcePromotionCampaignId`, `SourcePromotionRecipientId`,
                 `Nombre`, `Descripcion`, `Tipo`, `CategoriaEnvioEspecial`, `Valor`, `CodigoInterno`, `CodigoVisible`,
                 `TituloCanje`, `InstruccionesCanje`, `CondicionesUso`, `MensajeMarketing`, `DescuentoPorcentaje`,
                 `DescuentoImporteFijo`, `BeneficioEspecialResumen`, `BeneficioEspecialDetalle`, `GastoMinimoRequerido`,
                 `PuntosCoste`, `MaxUsosPorCliente`, `UsosConsumidos`, `ValidezDiasDesdeAsignacion`,
                 `RequiereValidacionManual`, `EsDeUnSoloUso`, `EsPlantilla`, `Activo`, `Publicado`, `Usado`,
                 `CreatedAtUtc`, `AvailableFromUtc`, `ExpiresAtUtc`, `UpdatedAtUtc`)
            SELECT UUID(), template.`NegocioId`, recipient.`UserId`, template.`Id`, campaign.`Id`, recipient.`Id`,
                   template.`Nombre`, template.`Descripcion`, 'Promocion', template.`CategoriaEnvioEspecial`,
                   template.`Valor`, template.`CodigoInterno`,
                   CONCAT(LEFT(COALESCE(NULLIF(template.`CodigoVisible`, ''), 'CAMPAIGN'), 8), '-', SUBSTRING(REPLACE(UUID(), '-', ''), 1, 11)),
                   template.`TituloCanje`, template.`InstruccionesCanje`, template.`CondicionesUso`, template.`MensajeMarketing`,
                   template.`DescuentoPorcentaje`, template.`DescuentoImporteFijo`, template.`BeneficioEspecialResumen`,
                   template.`BeneficioEspecialDetalle`, template.`GastoMinimoRequerido`, NULL,
                   template.`MaxUsosPorCliente`, 0, template.`ValidezDiasDesdeAsignacion`, template.`RequiereValidacionManual`,
                   template.`EsDeUnSoloUso`, 0, template.`Activo`, 0, 0, @now,
                   CASE
                       WHEN template.`AvailableFromUtc` IS NULL OR template.`AvailableFromUtc` < campaign.`StartsAtUtc`
                           THEN campaign.`StartsAtUtc`
                       ELSE template.`AvailableFromUtc`
                   END,
                   CASE
                       WHEN template.`ValidezDiasDesdeAsignacion` IS NOT NULL
                           THEN DATE_ADD(@now, INTERVAL template.`ValidezDiasDesdeAsignacion` DAY)
                       ELSE template.`ExpiresAtUtc`
                   END,
                   @now
            FROM `promotion_recipients` recipient
            INNER JOIN `promotion_campaigns` campaign ON campaign.`Id` = recipient.`CampaignId`
            INNER JOIN `fidelity_tickets` template ON template.`Id` = campaign.`TicketTemplateId`
            WHERE recipient.`CampaignId` = @campaignId
        """;

        await ExecuteCommandAsync(
            sql,
            [("@campaignId", campaignId.ToString()), ("@now", now)],
            cancellationToken);
    }

    private static void AppendFilters(
        StringBuilder where,
        List<(string Name, object? Value)> parameters,
        PromotionAudienceFiltersRequest filters,
        DateTime now)
    {
        if (filters.Genders is { Count: > 0 })
        {
            string[] parameterNames = filters.Genders
                .Distinct()
                .Select((gender, index) =>
                {
                    string name = $"@gender{index}";
                    parameters.Add((name, gender.ToString()));
                    return name;
                })
                .ToArray();
            where.Append($" AND user_account.`Gender` IN ({string.Join(", ", parameterNames)})");
        }

        if (filters.Provinces is { Count: > 0 })
        {
            string[] parameterNames = filters.Provinces
                .Distinct()
                .Select((province, index) =>
                {
                    string name = $"@province{index}";
                    parameters.Add((name, province.ToString()));
                    return name;
                })
                .ToArray();
            where.Append($" AND user_account.`Province` IN ({string.Join(", ", parameterNames)})");
        }

        AppendComparison(where, parameters, AgeExpression, ">=", "@minimumAge", filters.MinimumAge);
        AppendComparison(where, parameters, AgeExpression, "<=", "@maximumAge", filters.MaximumAge);
        AppendComparison(where, parameters, "COALESCE(points_data.`CurrentBalance`, 0)", ">=", "@minimumCurrentPoints", filters.MinimumCurrentPoints);
        AppendComparison(where, parameters, "COALESCE(points_data.`CurrentBalance`, 0)", "<=", "@maximumCurrentPoints", filters.MaximumCurrentPoints);
        AppendComparison(where, parameters, "COALESCE(points_data.`TotalEarned`, 0)", ">=", "@minimumTotalEarned", filters.MinimumTotalPointsEarned);
        AppendComparison(where, parameters, "COALESCE(points_data.`TotalEarned`, 0)", "<=", "@maximumTotalEarned", filters.MaximumTotalPointsEarned);
        AppendComparison(where, parameters, "COALESCE(points_data.`TotalSpent`, 0)", ">=", "@minimumTotalSpent", filters.MinimumTotalPointsSpent);
        AppendComparison(where, parameters, "COALESCE(points_data.`TotalSpent`, 0)", "<=", "@maximumTotalSpent", filters.MaximumTotalPointsSpent);

        AppendNullableDateFilter(where, parameters, filters, "<", "@lastEarnedBefore", filters.LastPointsEarnedBeforeUtc);
        AppendNullableDateFilter(where, parameters, filters, ">", "@lastEarnedAfter", filters.LastPointsEarnedAfterUtc);
        AppendComparison(where, parameters, "points_data.`LastSpentAtUtc`", "<=", "@lastSpentBefore", filters.LastPointsSpentBeforeUtc);
        AppendComparison(where, parameters, "points_data.`LastSpentAtUtc`", ">=", "@lastSpentAfter", filters.LastPointsSpentAfterUtc);

        if (filters.MinimumDaysSinceLastPointsEarned.HasValue)
        {
            AppendNullableDateFilter(
                where,
                parameters,
                filters,
                "<=",
                "@minimumDaysLastEarnedCutoff",
                now.AddDays(-filters.MinimumDaysSinceLastPointsEarned.Value));
        }

        if (filters.MaximumDaysSinceLastPointsEarned.HasValue)
        {
            parameters.Add(("@maximumDaysLastEarnedCutoff", now.AddDays(-filters.MaximumDaysSinceLastPointsEarned.Value)));
            where.Append(" AND points_data.`LastEarnedAtUtc` IS NOT NULL AND points_data.`LastEarnedAtUtc` >= @maximumDaysLastEarnedCutoff");
        }

        AppendComparison(where, parameters, LastActivityExpression, "<=", "@lastActivityBefore", filters.LastActivityBeforeUtc);
        AppendComparison(where, parameters, LastActivityExpression, ">=", "@lastActivityAfter", filters.LastActivityAfterUtc);
        AppendComparison(where, parameters, FirstActivityExpression, "<=", "@customerSinceBefore", filters.CustomerSinceBeforeUtc);
        AppendComparison(where, parameters, FirstActivityExpression, ">=", "@customerSinceAfter", filters.CustomerSinceAfterUtc);
        AppendComparison(where, parameters, "user_account.`CreatedAtUtc`", "<=", "@registeredBefore", filters.RegisteredBeforeUtc);
        AppendComparison(where, parameters, "user_account.`CreatedAtUtc`", ">=", "@registeredAfter", filters.RegisteredAfterUtc);
        AppendComparison(where, parameters, "user_account.`LastSeenAtUtc`", "<=", "@lastAppSeenBefore", filters.LastAppSeenBeforeUtc);
        AppendComparison(where, parameters, "user_account.`LastSeenAtUtc`", ">=", "@lastAppSeenAfter", filters.LastAppSeenAfterUtc);

        if (filters.MinimumDaysSinceLastAppSeen.HasValue)
        {
            AppendComparison(
                where,
                parameters,
                "user_account.`LastSeenAtUtc`",
                "<=",
                "@minimumDaysLastSeenCutoff",
                now.AddDays(-filters.MinimumDaysSinceLastAppSeen.Value));
        }

        if (filters.MaximumDaysSinceLastAppSeen.HasValue)
        {
            AppendComparison(
                where,
                parameters,
                "user_account.`LastSeenAtUtc`",
                ">=",
                "@maximumDaysLastSeenCutoff",
                now.AddDays(-filters.MaximumDaysSinceLastAppSeen.Value));
        }

        if (filters.BirthMonth.HasValue)
        {
            AppendComparison(where, parameters, "MONTH(user_account.`BirthDate`)", "=", "@birthMonth", filters.BirthMonth.Value);
        }

        AppendStringCollection(where, parameters, "user_account.`PostalCode`", "postalCode", filters.PostalCodes);
        AppendStringCollection(where, parameters, "user_account.`Region`", "region", filters.Regions);
        AppendStringCollection(where, parameters, "user_account.`CountryCode`", "countryCode", filters.CountryCodes);
        AppendStringCollection(where, parameters, "user_account.`Language`", "language", filters.Languages);

        if (filters.HasAnyPoints.HasValue)
        {
            where.Append(filters.HasAnyPoints.Value
                ? " AND points_data.`Id` IS NOT NULL"
                : " AND points_data.`Id` IS NULL");
        }

        if (filters.HasAnyTickets.HasValue)
        {
            where.Append(filters.HasAnyTickets.Value
                ? " AND ticket_stats.`UserId` IS NOT NULL"
                : " AND ticket_stats.`UserId` IS NULL");
        }

        if (filters.HasActiveTickets.HasValue)
        {
            parameters.Add(("@hasActiveTickets", filters.HasActiveTickets.Value ? 1 : 0));
            where.Append(" AND COALESCE(ticket_stats.`HasActiveTickets`, 0) = @hasActiveTickets");
        }

        if (filters.HasEverUsedTicket.HasValue)
        {
            where.Append(filters.HasEverUsedTicket.Value
                ? " AND COALESCE(ticket_stats.`UsedTickets`, 0) > 0"
                : " AND COALESCE(ticket_stats.`UsedTickets`, 0) = 0");
        }

        AppendComparison(where, parameters, "COALESCE(ticket_stats.`TotalTickets`, 0)", ">=", "@minimumTicketCount", filters.MinimumTicketCount);
        AppendComparison(where, parameters, "COALESCE(ticket_stats.`TotalTickets`, 0)", "<=", "@maximumTicketCount", filters.MaximumTicketCount);
        AppendComparison(where, parameters, "COALESCE(ticket_stats.`UsedTickets`, 0)", ">=", "@minimumUsedTicketCount", filters.MinimumUsedTicketCount);
        AppendComparison(where, parameters, "COALESCE(ticket_stats.`UsedTickets`, 0)", "<=", "@maximumUsedTicketCount", filters.MaximumUsedTicketCount);

        if (filters.HasActivePushNotifications.HasValue)
        {
            where.Append(filters.HasActivePushNotifications.Value
                ? " AND EXISTS (SELECT 1 FROM `user_devices` push_device WHERE push_device.`UserId` = candidates.`UserId` AND push_device.`NotificationsEnabled` = 1 AND push_device.`PushToken` IS NOT NULL AND push_device.`PushToken` <> '')"
                : " AND NOT EXISTS (SELECT 1 FROM `user_devices` push_device WHERE push_device.`UserId` = candidates.`UserId` AND push_device.`NotificationsEnabled` = 1 AND push_device.`PushToken` IS NOT NULL AND push_device.`PushToken` <> '')");
        }

        if (filters.HasConfirmedEmail.HasValue)
        {
            parameters.Add(("@hasConfirmedEmail", filters.HasConfirmedEmail.Value ? 1 : 0));
            where.Append(" AND user_account.`EmailConfirmed` = @hasConfirmedEmail");
        }
    }

    private static void AppendNullableDateFilter(
        StringBuilder where,
        List<(string Name, object? Value)> parameters,
        PromotionAudienceFiltersRequest filters,
        string comparison,
        string parameterName,
        DateTime? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        parameters.Add((parameterName, value.Value));
        where.Append(filters.IncludeUsersWithoutPointEarnings
            ? $" AND (points_data.`LastEarnedAtUtc` IS NULL OR points_data.`LastEarnedAtUtc` {comparison} {parameterName})"
            : $" AND points_data.`LastEarnedAtUtc` IS NOT NULL AND points_data.`LastEarnedAtUtc` {comparison} {parameterName}");
    }

    private static void AppendComparison(
        StringBuilder where,
        List<(string Name, object? Value)> parameters,
        string sqlExpression,
        string comparison,
        string parameterName,
        object? value)
    {
        if (value is null)
        {
            return;
        }

        parameters.Add((parameterName, value));
        where.Append($" AND {sqlExpression} {comparison} {parameterName}");
    }

    private static void AppendStringCollection(
        StringBuilder where,
        List<(string Name, object? Value)> parameters,
        string sqlExpression,
        string parameterPrefix,
        IReadOnlyCollection<string>? values)
    {
        if (values is not { Count: > 0 })
        {
            return;
        }

        string[] normalizedValues = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalizedValues.Length == 0)
        {
            return;
        }

        string[] parameterNames = normalizedValues
            .Select((value, index) =>
            {
                string name = $"@{parameterPrefix}{index}";
                parameters.Add((name, value));
                return name;
            })
            .ToArray();

        where.Append($" AND {sqlExpression} IN ({string.Join(", ", parameterNames)})");
    }

    private async Task ExecuteCommandAsync(
        string sql,
        IReadOnlyCollection<(string Name, object? Value)> parameters,
        CancellationToken cancellationToken)
    {
        DbConnection connection = _dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using DbCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        foreach ((string name, object? value) in parameters)
        {
            DbParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<IReadOnlyCollection<Ticket>> LoadAssignedTicketsAsync(
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT `Id`, `NegocioId`, `UserId`, `ParentTicketId`, `SourcePromotionCampaignId`,
                   `SourcePromotionRecipientId`, `Nombre`, `CategoriaEnvioEspecial`, `CreatedAtUtc`
            FROM `fidelity_tickets`
            WHERE `SourcePromotionCampaignId` = @campaignId
            """;

        await using DbCommand command = _dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = "@campaignId";
        parameter.Value = campaignId.ToString();
        command.Parameters.Add(parameter);

        if (command.Connection!.State != System.Data.ConnectionState.Open)
        {
            await command.Connection.OpenAsync(cancellationToken);
        }

        List<Ticket> tickets = [];
        await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tickets.Add(new Ticket
            {
                Id = reader.GetGuid(0),
                NegocioId = reader.GetGuid(1),
                UserId = reader.GetGuid(2),
                ParentTicketId = reader.GetGuid(3),
                SourcePromotionCampaignId = reader.GetGuid(4),
                SourcePromotionRecipientId = reader.GetGuid(5),
                Nombre = reader.GetString(6),
                CategoriaEnvioEspecial = Enum.Parse<CategoriaEnvioTicket>(reader.GetString(7)),
                CreatedAtUtc = reader.GetDateTime(8)
            });
        }

        return tickets;
    }

    private async Task<long> ExecuteScalarLongAsync(
        string sql,
        IReadOnlyCollection<(string Name, object? Value)> parameters,
        CancellationToken cancellationToken)
    {
        DbConnection connection = _dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using DbCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        foreach ((string name, object? value) in parameters)
        {
            DbParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        object? result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result);
    }

    private static int ToInt32Count(long count)
        => count > int.MaxValue ? int.MaxValue : (int)count;

    private const string LastActivityExpression =
        "GREATEST(COALESCE(points_data.`LastMovementAtUtc`, points_data.`UpdatedAtUtc`, '1000-01-01'), COALESCE(ticket_stats.`LastTicketAtUtc`, '1000-01-01'), COALESCE(candidates.`UltimaActividadUtc`, '1000-01-01'))";

    private const string FirstActivityExpression =
        "LEAST(COALESCE(points_data.`CreatedAtUtc`, '9999-12-31'), COALESCE(ticket_stats.`FirstTicketAtUtc`, '9999-12-31'), COALESCE(candidates.`FechaAltaUtc`, '9999-12-31'))";

    private const string AgeExpression =
        "TIMESTAMPDIFF(YEAR, user_account.`BirthDate`, @now)";

    private static JsonSerializerOptions CreateJsonOptions()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private sealed record AudienceSql(
        string FromWhereSql,
        IReadOnlyCollection<(string Name, object? Value)> Parameters);
}
