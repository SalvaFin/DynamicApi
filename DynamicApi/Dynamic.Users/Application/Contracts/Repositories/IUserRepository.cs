using Dynamic.Users.Domain.Entities;

namespace Dynamic.Users.Application.Contracts.Repositories;

public interface IUserRepository
{
    Task<UserAccount?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserAccount?> GetByIdentityAsync(string normalizedIdentity, CancellationToken cancellationToken = default);
    Task<UserAccount?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);
    Task<UserAccount?> GetByPhoneAsync(string normalizedPhoneNumber, CancellationToken cancellationToken = default);
    Task<UserAccount?> GetByUserNameAsync(string normalizedUserName, CancellationToken cancellationToken = default);
    Task<UserAccount?> GetByValidationTokenAsync(string validationToken, CancellationToken cancellationToken = default);
    Task AddAsync(UserAccount user, CancellationToken cancellationToken = default);
    void Update(UserAccount user);
    void Remove(UserAccount user);
}
