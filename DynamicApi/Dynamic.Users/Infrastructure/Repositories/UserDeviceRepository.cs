using Dynamic.Users.Application.Contracts.Repositories;
using Dynamic.Users.Domain.Entities;
using Dynamic.Users.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dynamic.Users.Infrastructure.Repositories;

public class UserDeviceRepository : IUserDeviceRepository
{
    private readonly DynamicUsersDbContext _dbContext;

    public UserDeviceRepository(DynamicUsersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<UserDevice?> GetByIdAsync(Guid deviceId, CancellationToken cancellationToken = default)
        => _dbContext.UserDevices.FirstOrDefaultAsync(device => device.Id == deviceId, cancellationToken);

    public Task<UserDevice?> GetBestMatchAsync(
        Guid userId,
        string? deviceFingerprint,
        string? externalDeviceId,
        string? installationId,
        CancellationToken cancellationToken = default)
        => _dbContext.UserDevices.FirstOrDefaultAsync(
            device => device.UserId == userId &&
                      ((!string.IsNullOrWhiteSpace(deviceFingerprint) && device.DeviceFingerprint == deviceFingerprint) ||
                       (!string.IsNullOrWhiteSpace(externalDeviceId) && device.ExternalDeviceId == externalDeviceId) ||
                       (!string.IsNullOrWhiteSpace(installationId) && device.InstallationId == installationId)),
            cancellationToken);

    public Task AddAsync(UserDevice device, CancellationToken cancellationToken = default)
        => _dbContext.UserDevices.AddAsync(device, cancellationToken).AsTask();

    public void Update(UserDevice device)
        => _dbContext.UserDevices.Update(device);
}
