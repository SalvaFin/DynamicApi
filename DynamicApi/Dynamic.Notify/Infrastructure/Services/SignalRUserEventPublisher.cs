using Dynamic.Notify.Application.Contracts;
using Dynamic.Notify.Application.Models;
using Dynamic.Notify.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Dynamic.Notify.Infrastructure.Services;

public class SignalRUserEventPublisher : IUserEventPublisher
{
    public const string ClientMethodName = "app.event";

    private readonly IHubContext<UserEventsHub> _hubContext;
    private readonly ILogger<SignalRUserEventPublisher> _logger;

    public SignalRUserEventPublisher(
        IHubContext<UserEventsHub> hubContext,
        ILogger<SignalRUserEventPublisher> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task PublishAsync(Guid userId, UserAppEvent appEvent, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(appEvent.Type))
        {
            return;
        }

        try
        {
            await _hubContext.Clients
                .User(userId.ToString())
                .SendAsync(ClientMethodName, appEvent, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "No se pudo publicar el evento en tiempo real {EventType} para el usuario {UserId}.",
                appEvent.Type,
                userId);
        }
    }
}
