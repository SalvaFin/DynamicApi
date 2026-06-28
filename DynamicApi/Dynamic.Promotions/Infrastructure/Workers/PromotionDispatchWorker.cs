using System.Text.Json;
using Dynamic.Fidelity.Application.DTOs.Responses;
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
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PromotionDispatchOptions _options;
    private readonly ILogger<PromotionDispatchWorker> _logger;

    public PromotionDispatchWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<PromotionDispatchOptions> options,
        ILogger<PromotionDispatchWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(_options.PollingIntervalSeconds));

        do
        {
            try
            {
                await ProcessOutboxAsync(stoppingToken);
                await ProcessPushDeliveriesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
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
                        DeepLink = "/mis-tickets"
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
}
