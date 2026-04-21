using Dynamic.Fidelity.Domain.Entities;

namespace Dynamic.Fidelity.Application.Contracts.Repositories;

public interface IUserCodeDirectoryRepository
{
    Task<UserCodeDirectoryEntry?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserCodeDirectoryEntry?> GetByUserCodeAsync(string userCode, CancellationToken cancellationToken = default);
    Task AddAsync(UserCodeDirectoryEntry entry, CancellationToken cancellationToken = default);
}
