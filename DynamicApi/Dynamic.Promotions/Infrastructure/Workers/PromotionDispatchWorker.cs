using System.Text.Json;
using System.Text.Json.Serialization;
using Dynamic.Fidelity.Application.DTOs.Responses;
using Dynamic.Notify.Application.Contracts;
using Dynamic.Notify.Application.Options;
using Dynamic.Negocios.Infrastructure.Persistence;
using Dynamic.Promotions.Application.Contracts;
using Dynamic.Promotions.Application.Models;
using Dynamic.Promotions.Application.Options;
using Dynamic.Promotions.Application.Services;
using Dynamic.Promotions.Domain.Entities;
using Dynamic.Promotions.Domain.Enums;
using Dynamic.Promotions.Infrastructure.Persistence;
using Dynamic.Users.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dynamic.Promotions.Infrastructure.Workers;

public class PromotionDispatchWorker : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PromotionDispatchOptions _options;
    private readonly PromotionEmailQueueTelemetry _emailTelemetry;
    private DateTime? _lastEmailTelemetryRefreshAtUtc;
    private readonly ILogger<PromotionDispatchWorker> _logger;

    public PromotionDispatchWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<PromotionDispatchOptions> options,
        PromotionEmailQueueTelemetry emailTelemetry,
        ILogger<PromotionDispatchWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _emailTelemetry = emailTelemetry;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(_options.PollingIntervalSeconds));

        do
        {
            _emailTelemetry.Heartbeat();
            try
            {
                await ProcessOutboxAsync(stoppingToken);
                await ProcessPushDeliveriesAsync(stoppingToken);
                await ProcessEmailDeliveriesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _emailTelemetry.RecordWorkerError(ex);
                _logger.LogError(ex, "Error inesperado en el worker de promociones.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ProcessOutboxAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        DynamicPromotionsDbContext dbContext = scope.ServiceProvider.GetRequiredService<DynamicPromotionsDbContext>();
        IPromotionAudienceBuilder audienceBuilder = scope.ServiceProvider.GetRequiredService<IPromotionAudienceBuilder>();
        DateTime now = DateTime.UtcNow;

        PromotionOutboxMessage? message = await dbContext.OutboxMessages
            .Where(item =>
                item.Type == PromotionService.BuildAudienceMessageType &&
                item.AvailableAtUtc <= now &&
                (item.Status == PromotionOutboxStatus.Pending ||
                 item.Status == PromotionOutboxStatus.Processing && item.ProcessingStartedAtUtc < now.AddMinutes(-10)))
            .OrderBy(item => item.AvailableAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (message is null)
        {
            return;
        }

        message.Status = PromotionOutboxStatus.Processing;
        message.ProcessingStartedAtUtc = now;
        message.AttemptCount++;
        message.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await audienceBuilder.BuildAsync(message.AggregateId, cancellationToken);
            message.Status = PromotionOutboxStatus.Completed;
            message.CompletedAtUtc = DateTime.UtcNow;
            message.UpdatedAtUtc = message.CompletedAtUtc.Value;
            message.LastError = null;
        }
        catch (Exception ex)
        {
            message.LastError = Truncate(ex.Message, 2000);
            message.UpdatedAtUtc = DateTime.UtcNow;
            if (message.AttemptCount >= 5)
            {
                message.Status = PromotionOutboxStatus.Failed;
            }
            else
            {
                message.Status = PromotionOutboxStatus.Pending;
                message.AvailableAtUtc = DateTime.UtcNow.Add(GetRetryDelay(message.AttemptCount));
            }

            _logger.LogError(ex, "No se pudo construir la audiencia de la campaña {CampaignId}.", message.AggregateId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ProcessPushDeliveriesAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        DynamicPromotionsDbContext promotionsDbContext = scope.ServiceProvider.GetRequiredService<DynamicPromotionsDbContext>();
        DynamicUsersDbContext usersDbContext = scope.ServiceProvider.GetRequiredService<DynamicUsersDbContext>();
        IPromotionPushSender pushSender = scope.ServiceProvider.GetRequiredService<IPromotionPushSender>();
        DateTime now = DateTime.UtcNow;

        await promotionsDbContext.Deliveries
            .Where(delivery =>
                delivery.Status == PromotionDeliveryStatus.Processing &&
                delivery.UpdatedAtUtc < now.AddMinutes(-10))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(delivery => delivery.Status, PromotionDeliveryStatus.Pending)
                .SetProperty(delivery => delivery.NextAttemptAtUtc, now)
                .SetProperty(delivery => delivery.UpdatedAtUtc, now),
                cancellationToken);

        IQueryable<Guid> expiredCampaignIds = promotionsDbContext.Campaigns
            .Where(campaign => campaign.ExpiresAtUtc <= now)
            .Select(campaign => campaign.Id);
        await promotionsDbContext.Deliveries
            .Where(delivery =>
                delivery.Status == PromotionDeliveryStatus.Pending &&
                expiredCampaignIds.Contains(delivery.CampaignId))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(delivery => delivery.Status, PromotionDeliveryStatus.Skipped)
                .SetProperty(delivery => delivery.LastError, "La promocion ha expirado antes del envio push.")
                .SetProperty(delivery => delivery.UpdatedAtUtc, now),
                cancellationToken);

        Guid[] candidateIds = await promotionsDbContext.Deliveries
            .AsNoTracking()
            .Where(delivery =>
                delivery.Status == PromotionDeliveryStatus.Pending &&
                delivery.NextAttemptAtUtc <= now &&
                delivery.Campaign.StartsAtUtc <= now &&
                delivery.Campaign.ExpiresAtUtc > now)
            .OrderBy(delivery => delivery.NextAttemptAtUtc)
            .Select(delivery => delivery.Id)
            .Take(_options.PushBatchSize)
            .ToArrayAsync(cancellationToken);

        List<PromotionDelivery> deliveries = [];
        foreach (Guid candidateId in candidateIds)
        {
            int claimed = await promotionsDbContext.Deliveries
                .Where(delivery =>
                    delivery.Id == candidateId &&
                    delivery.Status == PromotionDeliveryStatus.Pending)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(delivery => delivery.Status, PromotionDeliveryStatus.Processing)
                    .SetProperty(delivery => delivery.AttemptCount, delivery => delivery.AttemptCount + 1)
                    .SetProperty(delivery => delivery.UpdatedAtUtc, now),
                    cancellationToken);

            if (claimed == 0)
            {
                continue;
            }

            PromotionDelivery? delivery = await promotionsDbContext.Deliveries
                .Include(item => item.Campaign)
                .FirstOrDefaultAsync(item => item.Id == candidateId, cancellationToken);
            if (delivery is not null)
            {
                deliveries.Add(delivery);
            }
        }

        foreach (PromotionDelivery delivery in deliveries)
        {
            try
            {
                var device = await usersDbContext.UserDevices
                    .FirstOrDefaultAsync(item => item.Id == delivery.UserDeviceId && item.UserId == delivery.UserId, cancellationToken);

                if (device is null || !device.NotificationsEnabled || string.IsNullOrWhiteSpace(device.PushToken))
                {
                    delivery.Status = PromotionDeliveryStatus.Skipped;
                    delivery.LastError = "El dispositivo ya no admite notificaciones.";
                    delivery.UpdatedAtUtc = DateTime.UtcNow;
                    continue;
                }

                PromotionPushResult result = await pushSender.SendAsync(
                    new PromotionPushMessage
                    {
                        Token = device.PushToken,
                        Title = BuildPushTitle(delivery.Campaign),
                        Body = BuildPushBody(delivery.Campaign),
                        ImageUrl = delivery.Campaign.NegocioLogoUrlSnapshot,
                        PromotionRecipientId = delivery.RecipientId,
                        CampaignId = delivery.CampaignId,
                        NegocioId = delivery.Campaign.NegocioId,
                        DeepLink = "/portal/tickets"
                    },
                    cancellationToken);

                if (result.Succeeded)
                {
                    delivery.Status = PromotionDeliveryStatus.Delivered;
                    delivery.ProviderMessageId = result.ProviderMessageId;
                    delivery.DeliveredAtUtc = DateTime.UtcNow;
                    delivery.LastError = null;
                }
                else if (result.Retryable && delivery.AttemptCount < _options.MaxPushAttempts)
                {
                    delivery.Status = PromotionDeliveryStatus.Pending;
                    delivery.NextAttemptAtUtc = DateTime.UtcNow.Add(GetRetryDelay(delivery.AttemptCount));
                    delivery.LastError = result.Error;
                }
                else
                {
                    delivery.Status = result.InvalidToken
                        ? PromotionDeliveryStatus.Skipped
                        : PromotionDeliveryStatus.Failed;
                    delivery.LastError = result.Error;
                }

                if (result.InvalidToken)
                {
                    device.PushToken = null;
                    device.NotificationsEnabled = false;
                    device.UpdatedAtUtc = DateTime.UtcNow;
                }

                delivery.UpdatedAtUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                delivery.Status = delivery.AttemptCount < _options.MaxPushAttempts
                    ? PromotionDeliveryStatus.Pending
                    : PromotionDeliveryStatus.Failed;
                delivery.NextAttemptAtUtc = DateTime.UtcNow.Add(GetRetryDelay(delivery.AttemptCount));
                delivery.LastError = Truncate(ex.Message, 2000);
                delivery.UpdatedAtUtc = DateTime.UtcNow;
                _logger.LogError(ex, "Fallo enviando la promoción {DeliveryId}.", delivery.Id);
            }
        }

        await usersDbContext.SaveChangesAsync(cancellationToken);
        await promotionsDbContext.SaveChangesAsync(cancellationToken);

        Guid[] campaignIds = deliveries.Select(delivery => delivery.CampaignId).Distinct().ToArray();
        foreach (Guid campaignId in campaignIds)
        {
            PromotionCampaign? campaign = await promotionsDbContext.Campaigns.FirstOrDefaultAsync(
                item => item.Id == campaignId,
                cancellationToken);
            if (campaign is null)
            {
                continue;
            }

            campaign.PushDeliveredCount = await promotionsDbContext.Deliveries.CountAsync(
                item => item.CampaignId == campaignId && item.Status == PromotionDeliveryStatus.Delivered,
                cancellationToken);
            campaign.PushFailedCount = await promotionsDbContext.Deliveries.CountAsync(
                item => item.CampaignId == campaignId && item.Status == PromotionDeliveryStatus.Failed,
                cancellationToken);
            campaign.UpdatedAtUtc = DateTime.UtcNow;
        }

        if (campaignIds.Length > 0)
        {
            await promotionsDbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task ProcessEmailDeliveriesAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        DynamicPromotionsDbContext promotionsDbContext = scope.ServiceProvider.GetRequiredService<DynamicPromotionsDbContext>();
        DynamicUsersDbContext usersDbContext = scope.ServiceProvider.GetRequiredService<DynamicUsersDbContext>();
        DynamicNegociosDbContext negociosDbContext = scope.ServiceProvider.GetRequiredService<DynamicNegociosDbContext>();
        IEmailNotificationService emailService = scope.ServiceProvider.GetRequiredService<IEmailNotificationService>();
        PromotionEmailOptions emailOptions = scope.ServiceProvider.GetRequiredService<IOptions<PromotionEmailOptions>>().Value;
        SmtpOptions smtpOptions = scope.ServiceProvider.GetRequiredService<IOptions<SmtpOptions>>().Value;
        _emailTelemetry.SetSmtpEnabled(smtpOptions.Enabled);
        await RefreshEmailTelemetryAsync(promotionsDbContext, cancellationToken);
        if (!smtpOptions.Enabled)
        {
            return;
        }

        DateTime now = DateTime.UtcNow;
        int recoveredStaleLeases = await promotionsDbContext.EmailDeliveries
            .Where(delivery => delivery.Status == PromotionDeliveryStatus.Processing && delivery.UpdatedAtUtc < now.AddMinutes(-10))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(delivery => delivery.Status, PromotionDeliveryStatus.Pending)
                .SetProperty(delivery => delivery.NextAttemptAtUtc, now)
                .SetProperty(delivery => delivery.UpdatedAtUtc, now), cancellationToken);
        _emailTelemetry.RecordRecoveredStaleLeases(recoveredStaleLeases);

        IQueryable<Guid> expiredCampaignIds = promotionsDbContext.Campaigns
            .Where(campaign => campaign.ExpiresAtUtc <= now).Select(campaign => campaign.Id);
        await promotionsDbContext.EmailDeliveries
            .Where(delivery => delivery.Status == PromotionDeliveryStatus.Pending && expiredCampaignIds.Contains(delivery.CampaignId))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(delivery => delivery.Status, PromotionDeliveryStatus.Skipped)
                .SetProperty(delivery => delivery.LastError, "La promoción ha expirado antes del envío por correo.")
                .SetProperty(delivery => delivery.UpdatedAtUtc, now), cancellationToken);

        Guid[] candidateIds = await promotionsDbContext.EmailDeliveries.AsNoTracking()
            .Where(delivery => delivery.Status == PromotionDeliveryStatus.Pending &&
                               delivery.NextAttemptAtUtc <= now &&
                               delivery.Campaign.StartsAtUtc <= now &&
                               delivery.Campaign.ExpiresAtUtc > now)
            .OrderBy(delivery => delivery.NextAttemptAtUtc)
            .Select(delivery => delivery.Id)
            .Take(_options.EmailBatchSize)
            .ToArrayAsync(cancellationToken);

        HashSet<Guid> campaignIds = [];
        TimeSpan interval = TimeSpan.FromMilliseconds(60000d / _options.EmailsPerMinute);
        foreach (Guid candidateId in candidateIds)
        {
            int claimed = await promotionsDbContext.EmailDeliveries
                .Where(delivery => delivery.Id == candidateId && delivery.Status == PromotionDeliveryStatus.Pending)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(delivery => delivery.Status, PromotionDeliveryStatus.Processing)
                    .SetProperty(delivery => delivery.AttemptCount, delivery => delivery.AttemptCount + 1)
                    .SetProperty(delivery => delivery.UpdatedAtUtc, DateTime.UtcNow), cancellationToken);
            if (claimed == 0)
            {
                continue;
            }

            PromotionEmailDelivery? delivery = await promotionsDbContext.EmailDeliveries
                .Include(item => item.Campaign)
                .FirstOrDefaultAsync(item => item.Id == candidateId, cancellationToken);
            if (delivery is null)
            {
                continue;
            }

            campaignIds.Add(delivery.CampaignId);
            _emailTelemetry.StartDelivery(new PromotionEmailCurrentDelivery(
                delivery.Id,
                delivery.CampaignId,
                delivery.Campaign.NegocioId,
                delivery.Campaign.NegocioNombreSnapshot,
                delivery.Campaign.TicketNombreSnapshot,
                delivery.AttemptCount,
                DateTime.UtcNow));
            try
            {
                bool userStillAllowed = await usersDbContext.Users.AsNoTracking().AnyAsync(user =>
                    user.Id == delivery.UserId &&
                    user.Email == delivery.Email && user.Status == Dynamic.Users.Domain.Enums.UserStatus.Active,
                    cancellationToken);
                bool businessEmailStillAllowed = userStillAllowed &&
                    await negociosDbContext.NegociosAudiencias.AsNoTracking().AnyAsync(audience =>
                        audience.NegocioId == delivery.Campaign.NegocioId &&
                        audience.UserId == delivery.UserId &&
                        audience.Activa &&
                        audience.FechaBajaUtc == null &&
                        audience.PermiteCorreosPromocionales,
                        cancellationToken);
                if (!businessEmailStillAllowed)
                {
                    delivery.Status = PromotionDeliveryStatus.Skipped;
                    _emailTelemetry.CompleteSkipped(DateTime.UtcNow);
                    delivery.LastError = "El usuario ya no admite correos promocionales de este negocio en esta dirección.";
                }
                else
                {
                    TicketResponse? ticket = DeserializeTicket(delivery.Campaign);
                    await emailService.SendAsync(PromotionEmailTemplate.Build(delivery, ticket, emailOptions), cancellationToken);
                    delivery.Status = PromotionDeliveryStatus.Delivered;
                    delivery.DeliveredAtUtc = DateTime.UtcNow;
                    delivery.LastError = null;
                    _emailTelemetry.CompleteDelivered(delivery.DeliveredAtUtc.Value);
                }
            }
            catch (Exception ex)
            {
                delivery.Status = delivery.AttemptCount < _options.MaxEmailAttempts
                    ? PromotionDeliveryStatus.Pending
                    : PromotionDeliveryStatus.Failed;
                delivery.NextAttemptAtUtc = DateTime.UtcNow.Add(GetRetryDelay(delivery.AttemptCount));
                delivery.LastError = Truncate(ex.Message, 2000);
                _emailTelemetry.CompleteError(
                    new PromotionEmailRecentError(
                        DateTime.UtcNow,
                        delivery.Id,
                        delivery.CampaignId,
                        delivery.Campaign.NegocioId,
                        ClassifyEmailError(ex),
                        ex.Message,
                        delivery.AttemptCount,
                        delivery.Status == PromotionDeliveryStatus.Pending),
                    delivery.Status == PromotionDeliveryStatus.Failed);
                _logger.LogError(ex, "Fallo enviando la promoción por correo {EmailDeliveryId}.", delivery.Id);
            }

            delivery.UpdatedAtUtc = DateTime.UtcNow;
            await promotionsDbContext.SaveChangesAsync(cancellationToken);
            if (interval > TimeSpan.Zero)
            {
                await Task.Delay(interval, cancellationToken);
            }
        }

        foreach (Guid campaignId in campaignIds)
        {
            PromotionCampaign? campaign = await promotionsDbContext.Campaigns.FirstOrDefaultAsync(item => item.Id == campaignId, cancellationToken);
            if (campaign is null) continue;
            campaign.EmailDeliveredCount = await promotionsDbContext.EmailDeliveries.CountAsync(
                item => item.CampaignId == campaignId && item.Status == PromotionDeliveryStatus.Delivered, cancellationToken);
            campaign.EmailFailedCount = await promotionsDbContext.EmailDeliveries.CountAsync(
                item => item.CampaignId == campaignId && item.Status == PromotionDeliveryStatus.Failed, cancellationToken);
            campaign.UpdatedAtUtc = DateTime.UtcNow;
        }
        if (campaignIds.Count > 0)
        {
            await promotionsDbContext.SaveChangesAsync(cancellationToken);
        }
        await RefreshEmailTelemetryAsync(promotionsDbContext, cancellationToken);
    }

    private async Task RefreshEmailTelemetryAsync(
        DynamicPromotionsDbContext dbContext,
        CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        if (_lastEmailTelemetryRefreshAtUtc.HasValue &&
            now - _lastEmailTelemetryRefreshAtUtc.Value < TimeSpan.FromSeconds(_options.EmailTelemetryRefreshSeconds))
        {
            return;
        }
        IQueryable<PromotionEmailDelivery> pendingQuery = dbContext.EmailDeliveries.AsNoTracking()
            .Where(delivery => delivery.Status == PromotionDeliveryStatus.Pending);
        long pending = await pendingQuery.LongCountAsync(cancellationToken);
        long ready = await pendingQuery.LongCountAsync(delivery =>
            delivery.NextAttemptAtUtc <= now &&
            delivery.Campaign.StartsAtUtc <= now &&
            delivery.Campaign.ExpiresAtUtc > now, cancellationToken);
        long scheduled = await pendingQuery.LongCountAsync(delivery =>
            delivery.Campaign.ExpiresAtUtc > now &&
            (delivery.NextAttemptAtUtc > now || delivery.Campaign.StartsAtUtc > now), cancellationToken);
        long blocked = Math.Max(0, pending - ready - scheduled);
        long processing = await dbContext.EmailDeliveries.AsNoTracking()
            .LongCountAsync(delivery => delivery.Status == PromotionDeliveryStatus.Processing, cancellationToken);
        long failed = await dbContext.EmailDeliveries.AsNoTracking()
            .LongCountAsync(delivery => delivery.Status == PromotionDeliveryStatus.Failed, cancellationToken);
        long staleProcessing = await dbContext.EmailDeliveries.AsNoTracking().LongCountAsync(delivery =>
            delivery.Status == PromotionDeliveryStatus.Processing && delivery.UpdatedAtUtc < now.AddMinutes(-10),
            cancellationToken);
        DateTime? oldestReadyAtUtc = await pendingQuery
            .Where(delivery => delivery.NextAttemptAtUtc <= now &&
                               delivery.Campaign.StartsAtUtc <= now &&
                               delivery.Campaign.ExpiresAtUtc > now)
            .MinAsync(delivery => (DateTime?)delivery.CreatedAtUtc, cancellationToken);

        IQueryable<Guid> activeCampaignIds = dbContext.EmailDeliveries.AsNoTracking()
            .Where(delivery => delivery.Status == PromotionDeliveryStatus.Pending ||
                               delivery.Status == PromotionDeliveryStatus.Processing)
            .Select(delivery => delivery.CampaignId)
            .Distinct();
        var campaignRows = await dbContext.EmailDeliveries.AsNoTracking()
            .Where(delivery => activeCampaignIds.Contains(delivery.CampaignId))
            .GroupBy(delivery => new
            {
                delivery.CampaignId,
                delivery.Campaign.NegocioId,
                BusinessName = delivery.Campaign.NegocioNombreSnapshot,
                PromotionName = delivery.Campaign.TicketNombreSnapshot,
                delivery.Campaign.ExpiresAtUtc
            })
            .Select(group => new
            {
                group.Key.CampaignId,
                group.Key.NegocioId,
                group.Key.BusinessName,
                group.Key.PromotionName,
                group.Key.ExpiresAtUtc,
                Total = group.LongCount(),
                Pending = group.LongCount(item => item.Status == PromotionDeliveryStatus.Pending),
                Processing = group.LongCount(item => item.Status == PromotionDeliveryStatus.Processing),
                Delivered = group.LongCount(item => item.Status == PromotionDeliveryStatus.Delivered),
                Failed = group.LongCount(item => item.Status == PromotionDeliveryStatus.Failed),
                OldestPendingAtUtc = group.Where(item => item.Status == PromotionDeliveryStatus.Pending)
                    .Min(item => (DateTime?)item.CreatedAtUtc)
            })
            .OrderBy(row => row.OldestPendingAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);

        PromotionEmailActiveCampaign[] campaigns = campaignRows.Select(row =>
        {
            long completed = row.Total - row.Pending - row.Processing;
            decimal progress = row.Total == 0 ? 0 : decimal.Round(completed * 100m / row.Total, 2);
            return new PromotionEmailActiveCampaign(
                row.CampaignId,
                row.NegocioId,
                row.BusinessName,
                row.PromotionName,
                row.Total,
                row.Pending,
                row.Processing,
                row.Delivered,
                row.Failed,
                progress,
                row.OldestPendingAtUtc,
                row.ExpiresAtUtc);
        }).ToArray();

        _emailTelemetry.UpdateQueueSample(new PromotionEmailQueueDatabaseSample(
            now,
            pending,
            ready,
            scheduled,
            blocked,
            processing,
            failed,
            staleProcessing,
            oldestReadyAtUtc,
            campaigns));
        _lastEmailTelemetryRefreshAtUtc = now;
    }

    private static string ClassifyEmailError(Exception exception)
    {
        string typeName = exception.GetType().Name;
        if (typeName.Contains("Authentication", StringComparison.OrdinalIgnoreCase)) return "smtp-authentication";
        if (typeName.Contains("Command", StringComparison.OrdinalIgnoreCase)) return "smtp-command";
        if (typeName.Contains("Socket", StringComparison.OrdinalIgnoreCase) ||
            typeName.Contains("Connection", StringComparison.OrdinalIgnoreCase)) return "network";
        if (exception is TimeoutException) return "timeout";
        return "unexpected";
    }

    private static TimeSpan GetRetryDelay(int attempt)
    {
        double seconds = Math.Min(900, Math.Pow(2, Math.Clamp(attempt, 1, 10)) * 5);
        return TimeSpan.FromSeconds(seconds + Random.Shared.Next(0, 10));
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private static string BuildPushTitle(PromotionCampaign campaign)
    {
        TicketResponse? ticket = DeserializeTicket(campaign);
        return string.IsNullOrWhiteSpace(ticket?.Nombre)
            ? campaign.NegocioNombreSnapshot
            : $"{campaign.NegocioNombreSnapshot}: {ticket.Nombre}";
    }

    private static string BuildPushBody(PromotionCampaign campaign)
    {
        TicketResponse? ticket = DeserializeTicket(campaign);
        if (!string.IsNullOrWhiteSpace(ticket?.Descripcion))
        {
            return ticket.Descripcion;
        }

        return $"Has recibido un nuevo ticket de {campaign.NegocioNombreSnapshot}.";
    }

    private static TicketResponse? DeserializeTicket(PromotionCampaign campaign)
        => string.IsNullOrWhiteSpace(campaign.TicketSnapshotJson)
            ? null
            : JsonSerializer.Deserialize<TicketResponse>(campaign.TicketSnapshotJson, JsonOptions);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
