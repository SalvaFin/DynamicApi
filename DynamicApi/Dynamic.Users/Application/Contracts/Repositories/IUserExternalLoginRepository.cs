using Dynamic.Users.Domain.Entities;
using Dynamic.Users.Domain.Enums;

namespace Dynamic.Users.Application.Contracts.Repositories;

public interface IUserExternalLoginRepository
{
    Task<UserExternalLogin?> GetByProviderAsync(
        ExternalAuthProvider provider,
        string providerSubject,
        CancellationToken cancellationToken = default);

    Task AddAsync(UserExternalLogin externalLogin, CancellationToken cancellationToken = default);
    void Update(UserExternalLogin externalLogin);
}
