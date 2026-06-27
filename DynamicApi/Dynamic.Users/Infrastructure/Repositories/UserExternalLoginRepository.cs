using Dynamic.Users.Application.Contracts.Repositories;
using Dynamic.Users.Domain.Entities;
using Dynamic.Users.Domain.Enums;
using Dynamic.Users.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dynamic.Users.Infrastructure.Repositories;

public class UserExternalLoginRepository : IUserExternalLoginRepository
{
    private readonly DynamicUsersDbContext _dbContext;

    public UserExternalLoginRepository(DynamicUsersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<UserExternalLogin?> GetByProviderAsync(
        ExternalAuthProvider provider,
        string providerSubject,
        CancellationToken cancellationToken = default)
        => _dbContext.UserExternalLogins
            .Include(login => login.User)
            .FirstOrDefaultAsync(
                login => login.Provider == provider && login.ProviderSubject == providerSubject,
                cancellationToken);

    public Task AddAsync(UserExternalLogin externalLogin, CancellationToken cancellationToken = default)
        => _dbContext.UserExternalLogins.AddAsync(externalLogin, cancellationToken).AsTask();

    public void Update(UserExternalLogin externalLogin)
        => _dbContext.UserExternalLogins.Update(externalLogin);
}
