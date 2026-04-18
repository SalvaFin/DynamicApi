using Dynamic.Users.Domain.Entities;

namespace Dynamic.Users.Application.Contracts.Repositories;

public interface IUserDeviceRepository
{
    Task<UserDevice?> GetByIdAsync(Guid deviceId, CancellationToken cancellationToken = default);
    Task<UserDevice?> GetBestMatchAsync(
        Guid userId,
        string? deviceFingerprint,
        string? externalDeviceId,
        string? installationId,
        CancellationToken cancellationToken = default);
    Task AddAsync(UserDevice device, CancellationToken cancellationToken = default);
    void Update(UserDevice device);
}
