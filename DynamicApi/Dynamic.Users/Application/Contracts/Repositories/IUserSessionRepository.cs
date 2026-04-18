using Dynamic.Users.Domain.Entities;

namespace Dynamic.Users.Application.Contracts.Repositories;

public interface IUserSessionRepository
{
    Task<UserSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<UserSession?> GetByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<UserSession>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(UserSession session, CancellationToken cancellationToken = default);
    void Update(UserSession session);
}
