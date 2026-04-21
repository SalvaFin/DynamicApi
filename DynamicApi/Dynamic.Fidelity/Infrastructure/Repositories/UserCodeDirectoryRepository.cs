using Dynamic.Fidelity.Application.Contracts.Repositories;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dynamic.Fidelity.Infrastructure.Repositories;

public class UserCodeDirectoryRepository : IUserCodeDirectoryRepository
{
    private readonly DynamicFidelityDbContext _dbContext;

    public UserCodeDirectoryRepository(DynamicFidelityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<UserCodeDirectoryEntry?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => _dbContext.UserCodeDirectoryEntries.FirstOrDefaultAsync(entry => entry.UserId == userId, cancellationToken);

    public Task<UserCodeDirectoryEntry?> GetByUserCodeAsync(string userCode, CancellationToken cancellationToken = default)
        => _dbContext.UserCodeDirectoryEntries.FirstOrDefaultAsync(entry => entry.UserCode == userCode, cancellationToken);

    public Task AddAsync(UserCodeDirectoryEntry entry, CancellationToken cancellationToken = default)
        => _dbContext.UserCodeDirectoryEntries.AddAsync(entry, cancellationToken).AsTask();
}
