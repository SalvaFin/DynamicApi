using Dynamic.Users.Application.Contracts.Repositories;
using Dynamic.Users.Domain.Entities;
using Dynamic.Users.Infrastructure.Persistence;

namespace Dynamic.Users.Infrastructure.Repositories;

public class UserAuthEventRepository : IUserAuthEventRepository
{
    private readonly DynamicUsersDbContext _dbContext;

    public UserAuthEventRepository(DynamicUsersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(UserAuthEvent authEvent, CancellationToken cancellationToken = default)
        => _dbContext.UserAuthEvents.AddAsync(authEvent, cancellationToken).AsTask();
}
