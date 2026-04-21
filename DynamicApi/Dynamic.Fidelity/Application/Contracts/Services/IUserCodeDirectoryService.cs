namespace Dynamic.Fidelity.Application.Contracts.Services;

public interface IUserCodeDirectoryService
{
    Task<string> EnsureUserCodeAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<string?> GetUserCodeAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Guid?> ResolveUserIdAsync(string userCode, CancellationToken cancellationToken = default);
}
