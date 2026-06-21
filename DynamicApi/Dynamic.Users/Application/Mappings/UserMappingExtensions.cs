using Dynamic.Users.Application.DTOs.Responses;
using Dynamic.Users.Domain.Entities;
using Dynamic.Users.Domain.Enums;

namespace Dynamic.Users.Application.Mappings;

public static class UserMappingExtensions
{
    public static UserSummaryResponse ToResponse(this UserAccount user, string? userCode = null)
        => new()
        {
            Id = user.Id,
            UserCode = userCode,
            Email = user.Email,
            UserName = user.UserName,
            RequiresPasswordChange = user.PasswordIsTemporary,
            FirstName = user.FirstName,
            LastName = user.LastName,
            DisplayName = user.DisplayName,
            PhoneNumber = user.PhoneNumber,
            EmailConfirmed = user.EmailConfirmed,
            PhoneNumberConfirmed = user.PhoneNumberConfirmed,
            RegistrationCompleted = user.RegistrationCompleted,
            AgeAtRegistration = user.AgeAtRegistration,
            Role = user.Role,
            Status = user.Status,
            Language = user.Language,
            TimeZone = user.TimeZone,
            CountryCode = user.CountryCode,
            Region = user.Region,
            City = user.City,
            AvatarUrl = user.AvatarUrl,
            CreatedAtUtc = user.CreatedAtUtc,
            LastLoginAtUtc = user.LastLoginAtUtc
        };

    public static UserSessionResponse ToResponse(this UserSession session, Guid? currentSessionId = null)
        => new()
        {
            SessionId = session.Id,
            DeviceId = session.UserDeviceId,
            DeviceName = session.UserDevice?.DeviceName,
            DeviceType = session.UserDevice?.DeviceType ?? DeviceType.Unknown,
            Platform = session.UserDevice?.Platform ?? DevicePlatform.Unknown,
            AppName = session.UserDevice?.AppName ?? session.AppName,
            AppVersion = session.UserDevice?.AppVersion ?? session.AppVersion,
            NotificationsEnabled = session.UserDevice?.NotificationsEnabled ?? false,
            PushProvider = session.UserDevice?.PushProvider ?? PushProvider.None,
            CreatedAtUtc = session.CreatedAtUtc,
            LastSeenAtUtc = session.LastSeenAtUtc,
            ExpiresAtUtc = session.RefreshTokenExpiresAtUtc,
            IsCurrent = currentSessionId.HasValue && currentSessionId.Value == session.Id
        };
}
