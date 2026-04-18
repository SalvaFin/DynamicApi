using Dynamic.Users.Application.Contracts.Repositories;
using Dynamic.Users.Domain.Entities;
using Dynamic.Users.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dynamic.Users.Infrastructure.Repositories;

public class UserSessionRepository : IUserSessionRepository
{
    private readonly DynamicUsersDbContext _dbContext;

    public UserSessionRepository(DynamicUsersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<UserSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
        => _dbContext.UserSessions
            .Include(session => session.User)
            .Include(session => session.UserDevice)
            .FirstOrDefaultAsync(session => session.Id == sessionId, cancellationToken);

    public Task<UserSession?> GetByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken cancellationToken = default)
        => _dbContext.UserSessions
            .Include(session => session.User)
            .Include(session => session.UserDevice)
            .FirstOrDefaultAsync(session => session.RefreshTokenHash == refreshTokenHash, cancellationToken);

    public async Task<IReadOnlyCollection<UserSession>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        DateTime now = DateTime.UtcNow;

        return await _dbContext.UserSessions
            .Include(session => session.UserDevice)
            .Where(session => session.UserId == userId &&
                              session.RevokedAtUtc == null &&
                              session.RefreshTokenExpiresAtUtc > now)
            .OrderByDescending(session => session.LastSeenAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(UserSession session, CancellationToken cancellationToken = default)
        => _dbContext.UserSessions.AddAsync(session, cancellationToken).AsTask();

    public void Update(UserSession session)
        => _dbContext.UserSessions.Update(session);
}
