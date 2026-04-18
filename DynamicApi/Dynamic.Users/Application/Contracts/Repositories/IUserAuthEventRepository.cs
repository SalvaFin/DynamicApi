using Dynamic.Users.Domain.Entities;

namespace Dynamic.Users.Application.Contracts.Repositories;

public interface IUserAuthEventRepository
{
    Task AddAsync(UserAuthEvent authEvent, CancellationToken cancellationToken = default);
}
