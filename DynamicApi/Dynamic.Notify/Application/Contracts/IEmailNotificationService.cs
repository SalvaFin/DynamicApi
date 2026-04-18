using Dynamic.Notify.Application.Models;

namespace Dynamic.Notify.Application.Contracts;

public interface IEmailNotificationService
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
