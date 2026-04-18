using Dynamic.Users.Application.Contracts.Repositories;
using Dynamic.Users.Domain.Entities;
using Dynamic.Users.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dynamic.Users.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly DynamicUsersDbContext _dbContext;

    public UserRepository(DynamicUsersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<UserAccount?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => _dbContext.Users.FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);

    public Task<UserAccount?> GetByIdentityAsync(string normalizedIdentity, CancellationToken cancellationToken = default)
        => _dbContext.Users.FirstOrDefaultAsync(
            user => user.NormalizedEmail == normalizedIdentity ||
                    user.NormalizedUserName == normalizedIdentity ||
                    user.NormalizedPhoneNumber == normalizedIdentity,
            cancellationToken);

    public Task<UserAccount?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
        => _dbContext.Users.FirstOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);

    public Task<UserAccount?> GetByPhoneAsync(string normalizedPhoneNumber, CancellationToken cancellationToken = default)
        => _dbContext.Users.FirstOrDefaultAsync(user => user.NormalizedPhoneNumber == normalizedPhoneNumber, cancellationToken);

    public Task<UserAccount?> GetByUserNameAsync(string normalizedUserName, CancellationToken cancellationToken = default)
        => _dbContext.Users.FirstOrDefaultAsync(user => user.NormalizedUserName == normalizedUserName, cancellationToken);

    public Task<UserAccount?> GetByValidationTokenAsync(string validationToken, CancellationToken cancellationToken = default)
        => _dbContext.Users.FirstOrDefaultAsync(user => user.RegistrationValidationToken == validationToken, cancellationToken);

    public Task AddAsync(UserAccount user, CancellationToken cancellationToken = default)
        => _dbContext.Users.AddAsync(user, cancellationToken).AsTask();

    public void Update(UserAccount user)
        => _dbContext.Users.Update(user);
}
