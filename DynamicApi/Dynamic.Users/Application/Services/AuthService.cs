using Dynamic.Users.Application.Common;
using Dynamic.Users.Application.Contracts.Repositories;
using Dynamic.Users.Application.Contracts.Services;
using Dynamic.Users.Application.DTOs.Requests;
using Dynamic.Users.Application.DTOs.Responses;
using Dynamic.Users.Application.Mappings;
using Dynamic.Users.Application.Models;
using Dynamic.Users.Domain.Entities;
using Dynamic.Users.Domain.Enums;
using Dynamic.Users.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Dynamic.Users.Application.Services;

public class AuthService : IAuthService
{
    private const int MaxFailedLoginAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly DynamicUsersDbContext _dbContext;
    private readonly IUserRepository _userRepository;
    private readonly IUserDeviceRepository _userDeviceRepository;
    private readonly IUserSessionRepository _userSessionRepository;
    private readonly IUserAuthEventRepository _userAuthEventRepository;
    private readonly IPasswordHasher<UserAccount> _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        DynamicUsersDbContext dbContext,
        IUserRepository userRepository,
        IUserDeviceRepository userDeviceRepository,
        IUserSessionRepository userSessionRepository,
        IUserAuthEventRepository userAuthEventRepository,
        IPasswordHasher<UserAccount> passwordHasher,
        IJwtTokenService jwtTokenService,
        ILogger<AuthService> logger)
    {
        _dbContext = dbContext;
        _userRepository = userRepository;
        _userDeviceRepository = userDeviceRepository;
        _userSessionRepository = userSessionRepository;
        _userAuthEventRepository = userAuthEventRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    public async Task<ServiceResult<AuthResponse>> RegisterAsync(
        RegisterUserRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        string? validationMessage = ValidateRegistrationRequest(request);
        if (validationMessage is not null)
        {
            return ServiceResult<AuthResponse>.Failure("validation_error", validationMessage);
        }

        string normalizedEmail = Normalize(request.Email);
        string normalizedUserName = Normalize(request.UserName);

        if (await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken) is not null)
        {
            return ServiceResult<AuthResponse>.Failure("conflict", "El correo ya está registrado.");
        }

        if (await _userRepository.GetByUserNameAsync(normalizedUserName, cancellationToken) is not null)
        {
            return ServiceResult<AuthResponse>.Failure("conflict", "El nombre de usuario ya está en uso.");
        }

        DateTime now = DateTime.UtcNow;
        UserAccount user = new()
        {
            Id = Guid.NewGuid(),
            Email = request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            UserName = request.UserName.Trim(),
            NormalizedUserName = normalizedUserName,
            FirstName = NormalizeNullable(request.FirstName),
            LastName = NormalizeNullable(request.LastName),
            DisplayName = NormalizeNullable(request.DisplayName) ?? BuildDisplayName(request.FirstName, request.LastName, request.UserName),
            PhoneNumber = NormalizeNullable(request.PhoneNumber),
            BirthDate = request.BirthDate,
            Language = NormalizeNullable(request.Language),
            TimeZone = NormalizeNullable(request.TimeZone),
            CountryCode = NormalizeNullable(request.CountryCode)?.ToUpperInvariant(),
            Region = NormalizeNullable(request.Region),
            City = NormalizeNullable(request.City),
            TermsAccepted = request.AcceptTerms,
            PrivacyPolicyAccepted = request.AcceptPrivacyPolicy,
            MarketingAccepted = request.AcceptMarketing,
            TermsAcceptedAtUtc = now,
            PrivacyPolicyAcceptedAtUtc = now,
            MarketingAcceptedAtUtc = request.AcceptMarketing ? now : null,
            EmailConfirmed = false,
            Role = UserRole.Customer,
            Status = UserStatus.Active,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            LastSeenAtUtc = now,
            LastLoginAtUtc = now,
            LastLoginIp = ipAddress
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await _userRepository.AddAsync(user, cancellationToken);

            UserDevice? device = await CreateOrUpdateDeviceAsync(user, request.Client, now, cancellationToken);
            UserSession session = CreateSession(user, device, request.Client, ipAddress, userAgent, now);
            GeneratedTokenEnvelope tokens = _jwtTokenService.GenerateTokens(user, session);

            session.JwtId = tokens.JwtId;
            session.RefreshTokenHash = tokens.RefreshTokenHash;
            session.RefreshTokenExpiresAtUtc = tokens.RefreshTokenExpiresAtUtc;

            await _userSessionRepository.AddAsync(session, cancellationToken);
            await _userAuthEventRepository.AddAsync(BuildAuthEvent(AuthEventType.Register, user, request.Email, true, null, ipAddress, userAgent, request.Client, now), cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ServiceResult<AuthResponse>.Success(BuildAuthResponse(user, session, tokens));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error registrando usuario {Email}", request.Email);
            return ServiceResult<AuthResponse>.Failure("server_error", "No se pudo completar el registro.");
        }
    }

    public async Task<ServiceResult<AuthResponse>> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Identity) || string.IsNullOrWhiteSpace(request.Password))
        {
            return ServiceResult<AuthResponse>.Failure("validation_error", "Las credenciales son obligatorias.");
        }

        string normalizedIdentity = Normalize(request.Identity);
        UserAccount? user = await _userRepository.GetByIdentityAsync(normalizedIdentity, cancellationToken);
        DateTime now = DateTime.UtcNow;

        if (user is null)
        {
            await PersistAnonymousEventAsync(AuthEventType.LoginFailed, request.Identity, "Usuario no encontrado.", ipAddress, userAgent, request.Client, now, cancellationToken);
            return ServiceResult<AuthResponse>.Failure("unauthorized", "Credenciales inválidas.");
        }

        if (user.LockedUntilUtc.HasValue && user.LockedUntilUtc.Value > now)
        {
            return ServiceResult<AuthResponse>.Failure("locked", "La cuenta está temporalmente bloqueada por seguridad.");
        }

        PasswordVerificationResult verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            user.FailedLoginCount++;
            user.UpdatedAtUtc = now;

            string failureMessage = "Credenciales inválidas.";
            if (user.FailedLoginCount >= MaxFailedLoginAttempts)
            {
                user.LockedUntilUtc = now.Add(LockoutDuration);
                failureMessage = "Cuenta bloqueada temporalmente por múltiples intentos fallidos.";
            }

            await _userAuthEventRepository.AddAsync(
                BuildAuthEvent(AuthEventType.LoginFailed, user, request.Identity, false, failureMessage, ipAddress, userAgent, request.Client, now),
                cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<AuthResponse>.Failure("unauthorized", failureMessage);
        }

        user.FailedLoginCount = 0;
        user.LockedUntilUtc = null;
        user.LastLoginAtUtc = now;
        user.LastSeenAtUtc = now;
        user.LastLoginIp = ipAddress;
        user.UpdatedAtUtc = now;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            UserDevice? device = await CreateOrUpdateDeviceAsync(user, request.Client, now, cancellationToken);
            UserSession session = CreateSession(user, device, request.Client, ipAddress, userAgent, now);
            GeneratedTokenEnvelope tokens = _jwtTokenService.GenerateTokens(user, session);

            session.JwtId = tokens.JwtId;
            session.RefreshTokenHash = tokens.RefreshTokenHash;
            session.RefreshTokenExpiresAtUtc = tokens.RefreshTokenExpiresAtUtc;

            await _userSessionRepository.AddAsync(session, cancellationToken);
            await _userAuthEventRepository.AddAsync(
                BuildAuthEvent(AuthEventType.LoginSucceeded, user, request.Identity, true, null, ipAddress, userAgent, request.Client, now),
                cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ServiceResult<AuthResponse>.Success(BuildAuthResponse(user, session, tokens));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error iniciando sesión para {Identity}", request.Identity);
            return ServiceResult<AuthResponse>.Failure("server_error", "No se pudo iniciar sesión.");
        }
    }

    public async Task<ServiceResult<AuthResponse>> RefreshAsync(
        RefreshTokenRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return ServiceResult<AuthResponse>.Failure("validation_error", "El refresh token es obligatorio.");
        }

        string refreshTokenHash = _jwtTokenService.HashRefreshToken(request.RefreshToken);
        UserSession? session = await _userSessionRepository.GetByRefreshTokenHashAsync(refreshTokenHash, cancellationToken);
        DateTime now = DateTime.UtcNow;

        if (session is null || session.User is null)
        {
            await PersistAnonymousEventAsync(AuthEventType.RefreshFailed, null, "Refresh token inválido.", ipAddress, userAgent, request.Client, now, cancellationToken);
            return ServiceResult<AuthResponse>.Failure("unauthorized", "La sesión no es válida.");
        }

        if (request.SessionId.HasValue && request.SessionId.Value != session.Id)
        {
            return ServiceResult<AuthResponse>.Failure("unauthorized", "La sesión no coincide con el token de refresco.");
        }

        if (session.RevokedAtUtc.HasValue || session.RefreshTokenExpiresAtUtc <= now || session.User.Status != UserStatus.Active)
        {
            await _userAuthEventRepository.AddAsync(
                BuildAuthEvent(AuthEventType.RefreshFailed, session.User, session.User.Email, false, "Sesión expirada o revocada.", ipAddress, userAgent, request.Client, now),
                cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return ServiceResult<AuthResponse>.Failure("unauthorized", "La sesión ha expirado o ha sido revocada.");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            session.LastSeenAtUtc = now;
            session.IpAddress = ipAddress;
            session.UserAgent = userAgent;
            session.User.LastSeenAtUtc = now;
            session.User.UpdatedAtUtc = now;

            if (request.Client is not null)
            {
                UserDevice? device = await CreateOrUpdateDeviceAsync(session.User, request.Client, now, cancellationToken, session.UserDevice);
                if (device is not null)
                {
                    session.UserDeviceId = device.Id;
                    session.UserDevice = device;
                }
            }

            GeneratedTokenEnvelope tokens = _jwtTokenService.GenerateTokens(session.User, session);
            session.JwtId = tokens.JwtId;
            session.RefreshTokenHash = tokens.RefreshTokenHash;
            session.RefreshTokenExpiresAtUtc = tokens.RefreshTokenExpiresAtUtc;

            await _userAuthEventRepository.AddAsync(
                BuildAuthEvent(AuthEventType.RefreshSucceeded, session.User, session.User.Email, true, null, ipAddress, userAgent, request.Client, now),
                cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ServiceResult<AuthResponse>.Success(BuildAuthResponse(session.User, session, tokens));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error refrescando sesión {SessionId}", session.Id);
            return ServiceResult<AuthResponse>.Failure("server_error", "No se pudo refrescar la sesión.");
        }
    }

    public async Task<ServiceResult> LogoutAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        UserSession? session = await _userSessionRepository.GetByIdAsync(sessionId, cancellationToken);
        if (session is null || session.UserId != userId)
        {
            return ServiceResult.Failure("not_found", "La sesión no existe.");
        }

        if (session.RevokedAtUtc.HasValue)
        {
            return ServiceResult.Success();
        }

        DateTime now = DateTime.UtcNow;
        session.RevokedAtUtc = now;
        session.RevocationReason = "Logout";
        session.LastSeenAtUtc = now;

        await _userAuthEventRepository.AddAsync(
            BuildAuthEvent(AuthEventType.Logout, session.User, session.User?.Email, true, null, session.IpAddress, session.UserAgent, null, now),
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success();
    }

    private async Task PersistAnonymousEventAsync(
        AuthEventType eventType,
        string? identity,
        string? failureReason,
        string? ipAddress,
        string? userAgent,
        ClientDeviceContextRequest? client,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await _userAuthEventRepository.AddAsync(
            new UserAuthEvent
            {
                Id = Guid.NewGuid(),
                EventType = eventType,
                Identity = NormalizeNullable(identity),
                Succeeded = false,
                FailureReason = failureReason,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                ClientSummary = BuildClientSummary(client),
                CreatedAtUtc = now
            },
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<UserDevice?> CreateOrUpdateDeviceAsync(
        UserAccount user,
        ClientDeviceContextRequest? client,
        DateTime now,
        CancellationToken cancellationToken,
        UserDevice? existingDevice = null)
    {
        if (client is null && existingDevice is null)
        {
            return null;
        }

        string? fingerprint = NormalizeNullable(client?.DeviceFingerprint);
        string? externalDeviceId = NormalizeNullable(client?.DeviceId);
        string? installationId = NormalizeNullable(client?.InstallationId);

        UserDevice? device = existingDevice ?? await _userDeviceRepository.GetBestMatchAsync(
            user.Id,
            fingerprint,
            externalDeviceId,
            installationId,
            cancellationToken);

        if (device is null)
        {
            device = new UserDevice
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                DeviceFingerprint = fingerprint,
                ExternalDeviceId = externalDeviceId,
                InstallationId = installationId,
                FirstSeenAtUtc = now,
                LastSeenAtUtc = now,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            await _userDeviceRepository.AddAsync(device, cancellationToken);
        }
        else
        {
            device.LastSeenAtUtc = now;
            device.UpdatedAtUtc = now;
            _userDeviceRepository.Update(device);
        }

        device.DeviceName = NormalizeNullable(client?.DeviceName) ?? device.DeviceName ?? "Unknown device";
        device.DeviceType = client?.DeviceType ?? device.DeviceType;
        device.Platform = client?.Platform ?? device.Platform;
        device.Manufacturer = NormalizeNullable(client?.Manufacturer) ?? device.Manufacturer;
        device.Model = NormalizeNullable(client?.Model) ?? device.Model;
        device.OperatingSystem = NormalizeNullable(client?.OperatingSystem) ?? device.OperatingSystem;
        device.OperatingSystemVersion = NormalizeNullable(client?.OperatingSystemVersion) ?? device.OperatingSystemVersion;
        device.BrowserName = NormalizeNullable(client?.BrowserName) ?? device.BrowserName;
        device.BrowserVersion = NormalizeNullable(client?.BrowserVersion) ?? device.BrowserVersion;
        device.AppName = NormalizeNullable(client?.AppName) ?? device.AppName;
        device.AppVersion = NormalizeNullable(client?.AppVersion) ?? device.AppVersion;
        device.AppBuild = NormalizeNullable(client?.AppBuild) ?? device.AppBuild;
        device.Locale = NormalizeNullable(client?.Locale) ?? device.Locale;
        device.TimeZone = NormalizeNullable(client?.TimeZone) ?? device.TimeZone;
        device.NotificationsEnabled = client?.NotificationsEnabled ?? device.NotificationsEnabled;
        device.PushProvider = client?.PushProvider ?? device.PushProvider;

        if (!string.IsNullOrWhiteSpace(client?.PushToken))
        {
            device.PushToken = client.PushToken.Trim();
            device.PushTokenUpdatedAtUtc = now;
        }

        return device;
    }

    private static UserSession CreateSession(
        UserAccount user,
        UserDevice? device,
        ClientDeviceContextRequest? client,
        string? ipAddress,
        string? userAgent,
        DateTime now)
        => new()
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            UserDeviceId = device?.Id,
            UserDevice = device,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAtUtc = now,
            LastSeenAtUtc = now,
            AppName = NormalizeNullable(client?.AppName),
            AppVersion = NormalizeNullable(client?.AppVersion)
        };

    private static UserAuthEvent BuildAuthEvent(
        AuthEventType eventType,
        UserAccount? user,
        string? identity,
        bool succeeded,
        string? failureReason,
        string? ipAddress,
        string? userAgent,
        ClientDeviceContextRequest? client,
        DateTime now)
        => new()
        {
            Id = Guid.NewGuid(),
            UserId = user?.Id,
            EventType = eventType,
            Identity = NormalizeNullable(identity),
            Succeeded = succeeded,
            FailureReason = failureReason,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            ClientSummary = BuildClientSummary(client),
            CreatedAtUtc = now
        };

    private static string? BuildClientSummary(ClientDeviceContextRequest? client)
    {
        if (client is null)
        {
            return null;
        }

        string[] parts =
        [
            client.DeviceType.ToString(),
            client.Platform.ToString(),
            client.DeviceName ?? string.Empty,
            client.AppName ?? string.Empty,
            client.AppVersion ?? string.Empty
        ];

        string summary = string.Join(" | ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        return string.IsNullOrWhiteSpace(summary) ? null : summary;
    }

    private static AuthResponse BuildAuthResponse(UserAccount user, UserSession session, GeneratedTokenEnvelope tokens)
        => new()
        {
            AccessToken = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken,
            AccessTokenExpiresAtUtc = tokens.AccessTokenExpiresAtUtc,
            RefreshTokenExpiresAtUtc = tokens.RefreshTokenExpiresAtUtc,
            User = user.ToResponse(),
            CurrentSession = session.ToResponse(session.Id)
        };

    private static string? ValidateRegistrationRequest(RegisterUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            return "Email, usuario y contraseña son obligatorios.";
        }

        if (!request.AcceptTerms || !request.AcceptPrivacyPolicy)
        {
            return "Debes aceptar términos y política de privacidad.";
        }

        if (request.Password != request.ConfirmPassword)
        {
            return "La confirmación de contraseña no coincide.";
        }

        return request.Password.Length < 8
            ? "La contraseña debe tener al menos 8 caracteres."
            : null;
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string BuildDisplayName(string? firstName, string? lastName, string userName)
    {
        string fullName = $"{firstName} {lastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? userName.Trim() : fullName;
    }
}
