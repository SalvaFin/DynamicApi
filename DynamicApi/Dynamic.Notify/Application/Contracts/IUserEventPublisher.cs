using Dynamic.Notify.Application.Models;

namespace Dynamic.Notify.Application.Contracts;

public interface IUserEventPublisher
{
    Task PublishAsync(Guid userId, UserAppEvent appEvent, CancellationToken cancellationToken = default);
}
