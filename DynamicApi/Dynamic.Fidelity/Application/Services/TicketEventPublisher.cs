using Dynamic.Fidelity.Application.Contracts.Services;
using Dynamic.Fidelity.Application.Models;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Notify.Application.Contracts;
using Dynamic.Notify.Application.Models;

namespace Dynamic.Fidelity.Application.Services;

public class TicketEventPublisher : ITicketEventPublisher
{
    private readonly IUserEventPublisher _publisher;

    public TicketEventPublisher(IUserEventPublisher publisher) => _publisher = publisher;

    public Task PublishReceivedAsync(Ticket ticket, string source, CancellationToken cancellationToken = default)
    {
        if (!ticket.UserId.HasValue)
        {
            return Task.CompletedTask;
        }

        return _publisher.PublishAsync(ticket.UserId.Value, new UserAppEvent
        {
            Type = "fidelity.ticket.received",
            OccurredAtUtc = ticket.CreatedAtUtc,
            Payload = new TicketReceivedEventPayload
            {
                UserId = ticket.UserId.Value,
                NegocioId = ticket.NegocioId,
                TicketId = ticket.Id,
                ParentTicketId = ticket.ParentTicketId,
                SourceQrCampaignId = ticket.SourceQrCampaignId,
                SourcePromotionCampaignId = ticket.SourcePromotionCampaignId,
                SourcePromotionRecipientId = ticket.SourcePromotionRecipientId,
                Name = ticket.Nombre,
                Category = ticket.CategoriaEnvioEspecial.ToString(),
                Source = source,
                CreatedAtUtc = ticket.CreatedAtUtc
            }
        }, cancellationToken);
    }

    public Task PublishUsedAsync(Ticket ticket, Guid validatedByUserId, DateTime usedAtUtc, CancellationToken cancellationToken = default)
    {
        if (!ticket.UserId.HasValue)
        {
            return Task.CompletedTask;
        }

        return _publisher.PublishAsync(ticket.UserId.Value, new UserAppEvent
        {
            Type = "fidelity.ticket.used",
            OccurredAtUtc = usedAtUtc,
            Payload = new TicketUsedEventPayload
            {
                UserId = ticket.UserId.Value,
                NegocioId = ticket.NegocioId,
                TicketId = ticket.Id,
                ValidatedByUserId = validatedByUserId,
                Name = ticket.Nombre,
                UsageNumber = ticket.UsosConsumidos,
                FullyUsed = ticket.Usado,
                UsedAtUtc = usedAtUtc
            }
        }, cancellationToken);
    }
}
