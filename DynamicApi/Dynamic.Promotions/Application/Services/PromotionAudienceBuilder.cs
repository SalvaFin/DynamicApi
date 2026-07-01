using System.Data.Common;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dynamic.Promotions.Application.Contracts;
using Dynamic.Promotions.Application.DTOs.Requests;
using Dynamic.Promotions.Application.Options;
using Dynamic.Promotions.Domain.Entities;
using Dynamic.Promotions.Domain.Enums;
using Dynamic.Promotions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace Dynamic.Promotions.Application.Services;

public class PromotionAudienceBuilder : IPromotionAudienceBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly DynamicPromotionsDbContext _dbContext;
    private readonly PromotionDispatchOptions _dispatchOptions;
    private readonly FirebasePushOptions _firebaseOptions;

    public PromotionAudienceBuilder(
        DynamicPromotionsDbContext dbContext,
        IOptions<PromotionDispatchOptions> dispatchOptions,
        IOptions<FirebasePushOptions> firebaseOptions)
    {
        _dbContext = dbContext;
        _dispatchOptions = dispatchOptions.Value;
        _firebaseOptions = firebaseOptions.Value;
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

            campaign.AudienceCount = await _dbContext.Recipients
                .CountAsync(recipient => recipient.CampaignId == campaign.Id, cancellationToken);
            campaign.PushEligibleCount = await _dbContext.Deliveries
                .Select(delivery => new { delivery.CampaignId, delivery.RecipientId })
                .Where(delivery => delivery.CampaignId == campaign.Id)
                .Select(delivery => delivery.RecipientId)
                .Distinct()
                .CountAsync(cancellationToken);
            campaign.Status = PromotionCampaignStatus.Sent;
            campaign.AudienceProcessedAtUtc = now;
            campaign.UpdatedAtUtc = now;
            campaign.LastError = null;

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
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

    private async Task InsertRecipientsAsync(
        PromotionCampaign campaign,
        PromotionAudienceFiltersRequest filters,
        DateTime now,
        CancellationToken cancellationToken)
    {
        StringBuilder where = new();
        List<(string Name, object? Value)> parameters =
        [
            ("@campaignId", campaign.Id.ToString()),
            ("@negocioId", campaign.NegocioId.ToString()),
            ("@now", now),
            ("@expiresAt", campaign.ExpiresAtUtc),
            ("@businessCutoff", now.AddDays(-_dispatchOptions.MinimumDaysBetweenBusinessPromotions)),
            ("@globalCutoff", now.AddDays(-_dispatchOptions.GlobalPromotionWindowDays)),
            ("@globalLimit", _dispatchOptions.GlobalPromotionLimitPerWindow)
        ];

        AppendFilters(where, parameters, filters, now);

        string sql = $"""
            INSERT IGNORE INTO `promotion_recipients`
                (`Id`, `CampaignId`, `UserId`, `Status`, `ReceivedAtUtc`, `ExpiresAtUtc`, `CreatedAtUtc`, `UpdatedAtUtc`)
            SELECT UUID(), @campaignId, candidates.`UserId`, 'Received', @now, @expiresAt, @now, @now
            FROM (
                SELECT points_source.`UserId`
                FROM `fidelity_points` points_source
                WHERE points_source.`NegocioId` = @negocioId
                UNION
                SELECT ticket_source.`UserId`
                FROM `fidelity_tickets` ticket_source
                WHERE ticket_source.`NegocioId` = @negocioId
                  AND ticket_source.`UserId` IS NOT NULL
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
              AND user_account.`MarketingAccepted` = 1
              AND NOT EXISTS (
                  SELECT 1
                  FROM `negocio_user_links` staff_link
                  WHERE staff_link.`NegocioId` = @negocioId
                    AND staff_link.`UserId` = candidates.`UserId`
                    AND staff_link.`Activa` = 1
                    AND staff_link.`RevokedAtUtc` IS NULL
                    AND staff_link.`TipoVinculacion` <> 'Cliente'
              )
              AND NOT EXISTS (
                  SELECT 1
                  FROM `promotion_recipients` previous_recipient
                  INNER JOIN `promotion_campaigns` previous_campaign
                      ON previous_campaign.`Id` = previous_recipient.`CampaignId`
                  WHERE previous_recipient.`UserId` = candidates.`UserId`
                    AND previous_campaign.`NegocioId` = @negocioId
                    AND previous_recipient.`ReceivedAtUtc` >= @businessCutoff
              )
              AND (
                  SELECT COUNT(*)
                  FROM `promotion_recipients` global_recipient
                  WHERE global_recipient.`UserId` = candidates.`UserId`
                    AND global_recipient.`ReceivedAtUtc` >= @globalCutoff
              ) < @globalLimit
              {where}
            """;

        await ExecuteCommandAsync(sql, parameters, cancellationToken);
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
                   template.`Nombre`, template.`Descripcion`, template.`Tipo`, template.`CategoriaEnvioEspecial`,
                   template.`Valor`, template.`CodigoInterno`,
                   CONCAT(LEFT(COALESCE(NULLIF(template.`CodigoVisible`, ''), 'CAMPAIGN'), 8), '-', SUBSTRING(REPLACE(UUID(), '-', ''), 1, 11)),
                   template.`TituloCanje`, template.`InstruccionesCanje`, template.`CondicionesUso`, template.`MensajeMarketing`,
                   template.`DescuentoPorcentaje`, template.`DescuentoImporteFijo`, template.`BeneficioEspecialResumen`,
                   template.`BeneficioEspecialDetalle`, template.`GastoMinimoRequerido`, template.`PuntosCoste`,
                   template.`MaxUsosPorCliente`, 0, template.`ValidezDiasDesdeAsignacion`, template.`RequiereValidacionManual`,
                   template.`EsDeUnSoloUso`, 0, template.`Activo`, template.`Publicado`, 0, @now,
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

    private const string LastActivityExpression =
        "GREATEST(COALESCE(points_data.`LastMovementAtUtc`, points_data.`UpdatedAtUtc`, '1000-01-01'), COALESCE(ticket_stats.`LastTicketAtUtc`, '1000-01-01'))";

    private const string FirstActivityExpression =
        "LEAST(COALESCE(points_data.`CreatedAtUtc`, '9999-12-31'), COALESCE(ticket_stats.`FirstTicketAtUtc`, '9999-12-31'))";

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
}
