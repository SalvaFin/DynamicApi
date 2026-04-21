using Dynamic.Users.Application.Common;
using Dynamic.Users.Application.Contracts.Repositories;
using Dynamic.Users.Application.Contracts.Services;
using Dynamic.Users.Application.DTOs.Requests;
using Dynamic.Users.Application.DTOs.Responses;
using Dynamic.Users.Application.Mappings;
using Dynamic.Users.Infrastructure.Persistence;
using Dynamic.Fidelity.Application.Contracts.Services;

namespace Dynamic.Users.Application.Services;

public class UserService : IUserService
{
    private readonly DynamicUsersDbContext _dbContext;
    private readonly IUserRepository _userRepository;
    private readonly IUserSessionRepository _userSessionRepository;
    private readonly IUserDeviceRepository _userDeviceRepository;
    private readonly IUserCodeDirectoryService _userCodeDirectoryService;

    public UserService(
        DynamicUsersDbContext dbContext,
        IUserRepository userRepository,
        IUserSessionRepository userSessionRepository,
        IUserDeviceRepository userDeviceRepository,
        IUserCodeDirectoryService userCodeDirectoryService)
    {
        _dbContext = dbContext;
        _userRepository = userRepository;
        _userSessionRepository = userSessionRepository;
        _userDeviceRepository = userDeviceRepository;
        _userCodeDirectoryService = userCodeDirectoryService;
    }

    public async Task<ServiceResult<UserSummaryResponse>> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        Domain.Entities.UserAccount? user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return ServiceResult<UserSummaryResponse>.Failure("not_found", "Usuario no encontrado.");
        }

        string userCode = await _userCodeDirectoryService.EnsureUserCodeAsync(user.Id, cancellationToken);
        return ServiceResult<UserSummaryResponse>.Success(user.ToResponse(userCode));
    }

    public async Task<ServiceResult<IReadOnlyCollection<UserSessionResponse>>> GetActiveSessionsAsync(
        Guid userId,
        Guid currentSessionId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<Domain.Entities.UserSession> sessions = await _userSessionRepository.GetActiveByUserIdAsync(userId, cancellationToken);
        IReadOnlyCollection<UserSessionResponse> result = sessions
            .Select(session => session.ToResponse(currentSessionId))
            .ToArray();

        return ServiceResult<IReadOnlyCollection<UserSessionResponse>>.Success(result);
    }

    public async Task<ServiceResult<UserSessionResponse>> UpdatePushTokenAsync(
        Guid userId,
        Guid sessionId,
        UpdatePushTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        Domain.Entities.UserSession? session = await _userSessionRepository.GetByIdAsync(sessionId, cancellationToken);
        if (session is null || session.UserId != userId)
        {
            return ServiceResult<UserSessionResponse>.Failure("not_found", "La sesión no existe.");
        }

        Domain.Entities.UserDevice? device = session.UserDevice;
        if (device is null)
        {
            device = await _userDeviceRepository.GetBestMatchAsync(
                userId,
                NormalizeNullable(request.DeviceFingerprint),
                NormalizeNullable(request.DeviceId),
                NormalizeNullable(request.InstallationId),
                cancellationToken);
        }

        if (device is null)
        {
            return ServiceResult<UserSessionResponse>.Failure("not_found", "No se ha encontrado el dispositivo asociado.");
        }

        DateTime now = DateTime.UtcNow;
        device.PushToken = NormalizeNullable(request.PushToken);
        device.PushProvider = request.PushProvider;
        device.NotificationsEnabled = request.NotificationsEnabled;
        device.AppVersion = NormalizeNullable(request.AppVersion) ?? device.AppVersion;
        device.AppBuild = NormalizeNullable(request.AppBuild) ?? device.AppBuild;
        device.PushTokenUpdatedAtUtc = string.IsNullOrWhiteSpace(request.PushToken) ? device.PushTokenUpdatedAtUtc : now;
        device.LastSeenAtUtc = now;
        device.UpdatedAtUtc = now;
        _userDeviceRepository.Update(device);

        session.UserDeviceId = device.Id;
        session.UserDevice = device;
        session.LastSeenAtUtc = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<UserSessionResponse>.Success(session.ToResponse(sessionId));
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
