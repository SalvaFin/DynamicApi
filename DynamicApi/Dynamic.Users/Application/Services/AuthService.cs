using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Dynamic.Fidelity.Application.Contracts.Services;
using Dynamic.Notify.Application.Contracts;
using Dynamic.Notify.Application.Models;
using Dynamic.Users.Application.Common;
using Dynamic.Users.Application.Contracts.Repositories;
using Dynamic.Users.Application.Contracts.Services;
using Dynamic.Users.Application.DTOs.Requests;
using Dynamic.Users.Application.DTOs.Responses;
using Dynamic.Users.Application.Mappings;
using Dynamic.Users.Application.Models;
using Dynamic.Users.Application.Options;
using Dynamic.Users.Domain.Entities;
using Dynamic.Users.Domain.Enums;
using Dynamic.Users.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    private readonly IEmailNotificationService _emailNotificationService;
    private readonly IRegistrationRewardService _registrationRewardService;
    private readonly IUserCodeDirectoryService _userCodeDirectoryService;
    private readonly UserRegistrationOptions _userRegistrationOptions;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        DynamicUsersDbContext dbContext,
        IUserRepository userRepository,
        IUserDeviceRepository userDeviceRepository,
        IUserSessionRepository userSessionRepository,
        IUserAuthEventRepository userAuthEventRepository,
        IPasswordHasher<UserAccount> passwordHasher,
        IJwtTokenService jwtTokenService,
        IEmailNotificationService emailNotificationService,
        IRegistrationRewardService registrationRewardService,
        IUserCodeDirectoryService userCodeDirectoryService,
        IOptions<UserRegistrationOptions> userRegistrationOptions,
        ILogger<AuthService> logger)
    {
        _dbContext = dbContext;
        _userRepository = userRepository;
        _userDeviceRepository = userDeviceRepository;
        _userSessionRepository = userSessionRepository;
        _userAuthEventRepository = userAuthEventRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _emailNotificationService = emailNotificationService;
        _registrationRewardService = registrationRewardService;
        _userCodeDirectoryService = userCodeDirectoryService;
        _userRegistrationOptions = userRegistrationOptions.Value;
        _logger = logger;
    }

    public async Task<ServiceResult<RegisterStartResponse>> StartRegistrationAsync(
        RegisterStartRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Contact))
        {
            return ServiceResult<RegisterStartResponse>.Failure("validation_error", "Debes indicar un email o un número de teléfono.");
        }

        ContactInfo? contactInfo = ParseContact(request.Contact);
        if (contactInfo is null)
        {
            return ServiceResult<RegisterStartResponse>.Failure("validation_error", "El contacto indicado no es un email ni un teléfono válido.");
        }

        if (!string.IsNullOrWhiteSpace(request.QrToken))
        {
            bool isQrTokenValid = await _registrationRewardService.ValidateQrTokenAsync(request.QrToken, cancellationToken);
            if (!isQrTokenValid)
            {
                return ServiceResult<RegisterStartResponse>.Failure("validation_error", "El QR de registro no es válido o ya no está disponible.");
            }
        }

        UserAccount? existingUser = contactInfo.Type switch
        {
            ContactType.Email => await _userRepository.GetByEmailAsync(contactInfo.NormalizedValue, cancellationToken),
            ContactType.Phone => await _userRepository.GetByPhoneAsync(contactInfo.NormalizedValue, cancellationToken),
            _ => null
        };

        if (existingUser is not null && existingUser.RegistrationCompleted)
        {
            return ServiceResult<RegisterStartResponse>.Success(new RegisterStartResponse
            {
                AlreadyExists = true,
                NotificationSent = false,
                PendingRegistrationCreated = false,
                ShouldRedirectToLogin = true,
                DeliveryChannel = contactInfo.Type == ContactType.Email ? "email" : "phone",
                NextAction = "login",
                Message = "El usuario ya existe. Debes iniciar sesión.",
                Contact = request.Contact.Trim(),
                UserName = existingUser.UserName
            });
        }

        DateTime now = DateTime.UtcNow;
        string validationToken = Guid.NewGuid().ToString("N");
        string temporaryPassword = GenerateTemporaryPassword();
        string generatedUserName = existingUser?.UserName ?? $"user{Guid.NewGuid():N}"[..16];

        UserAccount user = existingUser ?? new UserAccount
        {
            Id = Guid.NewGuid(),
            UserName = generatedUserName,
            NormalizedUserName = generatedUserName.ToUpperInvariant(),
            Role = UserRole.User,
            Status = UserStatus.PendingActivation,
            CreatedAtUtc = now
        };

        if (contactInfo.Type == ContactType.Email)
        {
            user.Email = contactInfo.OriginalValue;
            user.NormalizedEmail = contactInfo.NormalizedValue;
        }
        else
        {
            user.PhoneNumber = contactInfo.OriginalValue;
            user.NormalizedPhoneNumber = contactInfo.NormalizedValue;
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, temporaryPassword);
        user.EmailConfirmed = false;
        user.PhoneNumberConfirmed = false;
        user.RegistrationCompleted = false;
        user.AgeAtRegistration = null;
        user.FirstName = existingUser?.FirstName;
        user.LastName = existingUser?.LastName;
        user.DisplayName = existingUser?.DisplayName;
        user.RegistrationValidationToken = validationToken;
        user.RegistrationValidationTokenExpiresAtUtc = now.AddHours(_userRegistrationOptions.ValidationTokenExpirationHours);
        user.RegistrationInitiatedAtUtc = now;
        user.RegistrationCompletedAtUtc = null;
        user.TemporaryPasswordSentAtUtc = contactInfo.Type == ContactType.Email ? now : null;
        user.UpdatedAtUtc = now;
        user.LastSeenAtUtc = now;
        user.LastLoginIp = ipAddress;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            if (existingUser is null)
            {
                await _userRepository.AddAsync(user, cancellationToken);
            }
            else
            {
                _userRepository.Update(user);
            }

            await _userAuthEventRepository.AddAsync(
                BuildAuthEvent(AuthEventType.RegisterStarted, user, contactInfo.OriginalValue, true, null, ipAddress, userAgent, request.Client, now),
                cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);

            bool notificationSent = false;
            string deliveryChannel = contactInfo.Type == ContactType.Email ? "email" : "whatsapp_pending";
            string nextAction = contactInfo.Type == ContactType.Email ? "complete_registration" : "wait_whatsapp_implementation";
            string message;

            if (contactInfo.Type == ContactType.Email)
            {
                string completionLink = BuildCompletionLink(contactInfo.OriginalValue, validationToken);
                await _emailNotificationService.SendAsync(
                    new EmailMessage
                    {
                        ToEmail = contactInfo.OriginalValue,
                        ToName = user.DisplayName ?? user.UserName,
                        Subject = "Completa tu registro en Dynamic",
                        HtmlBody = BuildRegistrationEmailHtml(user.UserName, temporaryPassword, completionLink),
                        TextBody = BuildRegistrationEmailText(user.UserName, temporaryPassword, completionLink)
                    },
                    cancellationToken);

                notificationSent = true;
                message = "Te hemos enviado un correo con el enlace para completar el registro y tu contraseña temporal.";
            }
            else
            {
                message = "El registro por teléfono ha quedado preparado, pero el envío por WhatsApp aún no está implementado.";
            }

            if (!string.IsNullOrWhiteSpace(request.QrToken))
            {
                try
                {
                    await _registrationRewardService.PreparePendingAssignmentAsync(user.Id, request.QrToken, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "No se pudo preparar la recompensa de registro para el usuario {UserId}", user.Id);
                }
            }

            await _userCodeDirectoryService.EnsureUserCodeAsync(user.Id, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return ServiceResult<RegisterStartResponse>.Success(new RegisterStartResponse
            {
                AlreadyExists = false,
                PendingRegistrationCreated = true,
                NotificationSent = notificationSent,
                ShouldRedirectToLogin = false,
                DeliveryChannel = deliveryChannel,
                NextAction = nextAction,
                Message = message,
                Contact = contactInfo.OriginalValue,
                UserName = user.UserName
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error iniciando registro para {Contact}", request.Contact);
            return ServiceResult<RegisterStartResponse>.Failure("server_error", "No se pudo iniciar el registro.");
        }
    }

    public async Task<ServiceResult<CompleteRegistrationResponse>> CompleteRegistrationAsync(
        CompleteRegistrationRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Contact) ||
            string.IsNullOrWhiteSpace(request.ValidationToken) ||
            string.IsNullOrWhiteSpace(request.Nombre) ||
            string.IsNullOrWhiteSpace(request.Apellidos))
        {
            return ServiceResult<CompleteRegistrationResponse>.Failure("validation_error", "Faltan datos para completar el registro.");
        }

        if (request.Edad < _userRegistrationOptions.MinimumAge)
        {
            return ServiceResult<CompleteRegistrationResponse>.Failure("validation_error", $"La edad mínima para registrarse es de {_userRegistrationOptions.MinimumAge} años.");
        }

        ContactInfo? contactInfo = ParseContact(request.Contact);
        if (contactInfo is null)
        {
            return ServiceResult<CompleteRegistrationResponse>.Failure("validation_error", "El contacto indicado no es válido.");
        }

        UserAccount? userByContact = contactInfo.Type switch
        {
            ContactType.Email => await _userRepository.GetByEmailAsync(contactInfo.NormalizedValue, cancellationToken),
            ContactType.Phone => await _userRepository.GetByPhoneAsync(contactInfo.NormalizedValue, cancellationToken),
            _ => null
        };

        if (userByContact is null)
        {
            return ServiceResult<CompleteRegistrationResponse>.Failure("not_found", "No existe un registro pendiente para ese contacto.");
        }

        if (!string.Equals(userByContact.RegistrationValidationToken, request.ValidationToken.Trim(), StringComparison.Ordinal))
        {
            return ServiceResult<CompleteRegistrationResponse>.Failure("unauthorized", "El token de validación no es correcto.");
        }

        if (!userByContact.RegistrationValidationTokenExpiresAtUtc.HasValue ||
            userByContact.RegistrationValidationTokenExpiresAtUtc.Value < DateTime.UtcNow)
        {
            return ServiceResult<CompleteRegistrationResponse>.Failure("unauthorized", "El token de validación ha expirado.");
        }

        DateTime now = DateTime.UtcNow;
        userByContact.FirstName = request.Nombre.Trim();
        userByContact.LastName = request.Apellidos.Trim();
        userByContact.DisplayName = $"{request.Nombre} {request.Apellidos}".Trim();
        userByContact.AgeAtRegistration = request.Edad;
        userByContact.RegistrationCompleted = true;
        userByContact.RegistrationCompletedAtUtc = now;
        userByContact.RegistrationValidationToken = null;
        userByContact.RegistrationValidationTokenExpiresAtUtc = null;
        userByContact.Status = UserStatus.Active;
        userByContact.LastLoginAtUtc = now;
        userByContact.LastSeenAtUtc = now;
        userByContact.LastLoginIp = ipAddress;
        userByContact.UpdatedAtUtc = now;

        if (contactInfo.Type == ContactType.Email)
        {
            userByContact.EmailConfirmed = true;
        }
        else
        {
            userByContact.PhoneNumberConfirmed = true;
        }

        string userCode = await _userCodeDirectoryService.EnsureUserCodeAsync(userByContact.Id, cancellationToken);
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        bool committed = false;

        try
        {
            UserDevice? device = await CreateOrUpdateDeviceAsync(userByContact, request.Client, now, cancellationToken);
            UserSession session = CreateSession(userByContact, device, request.Client, ipAddress, userAgent, now);
            GeneratedTokenEnvelope tokens = _jwtTokenService.GenerateTokens(userByContact, session);

            session.JwtId = tokens.JwtId;
            session.RefreshTokenHash = tokens.RefreshTokenHash;
            session.RefreshTokenExpiresAtUtc = tokens.RefreshTokenExpiresAtUtc;

            await _userSessionRepository.AddAsync(session, cancellationToken);
            await _userAuthEventRepository.AddAsync(
                BuildAuthEvent(AuthEventType.RegisterCompleted, userByContact, contactInfo.OriginalValue, true, null, ipAddress, userAgent, request.Client, now),
                cancellationToken);
            await _userAuthEventRepository.AddAsync(
                BuildAuthEvent(AuthEventType.LoginSucceeded, userByContact, contactInfo.OriginalValue, true, null, ipAddress, userAgent, request.Client, now),
                cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            committed = true;

            try
            {
                await _registrationRewardService.FinalizePendingAssignmentsAsync(userByContact.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudieron finalizar las recompensas pendientes de registro para {UserId}", userByContact.Id);
            }

            return ServiceResult<CompleteRegistrationResponse>.Success(new CompleteRegistrationResponse
            {
                Completed = true,
                Contact = contactInfo.OriginalValue,
                UserName = userByContact.UserName,
                LoggedIn = true,
                Auth = BuildAuthResponse(userByContact, userCode, session, tokens),
                Message = "Registro completado correctamente. Sesion iniciada."
            });
        }
        catch (Exception ex)
        {
            if (!committed)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            _logger.LogError(ex, "Error completando registro para {Contact}", request.Contact);
            return ServiceResult<CompleteRegistrationResponse>.Failure("server_error", "No se pudo completar el registro.");
        }

    }

    public async Task<ServiceResult<UserSummaryResponse>> ClassicRegisterAsync(
        ClassicRegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.ConfirmPassword))
        {
            return ServiceResult<UserSummaryResponse>.Failure("validation_error", "UserName, contraseña y confirmación son obligatorios.");
        }

        if (request.Password != request.ConfirmPassword)
        {
            return ServiceResult<UserSummaryResponse>.Failure("validation_error", "La confirmación de contraseña no coincide.");
        }

        if (string.IsNullOrWhiteSpace(request.Email) && string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return ServiceResult<UserSummaryResponse>.Failure("validation_error", "Debes indicar al menos un email o un número de teléfono.");
        }

        string normalizedUserName = request.UserName.Trim().ToUpperInvariant();
        if (await _userRepository.GetByUserNameAsync(normalizedUserName, cancellationToken) is not null)
        {
            return ServiceResult<UserSummaryResponse>.Failure("conflict", "Ya existe un usuario con ese nombre.");
        }

        ContactInfo? emailContact = null;
        ContactInfo? phoneContact = null;

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            emailContact = ParseContact(request.Email);
            if (emailContact is null || emailContact.Type != ContactType.Email)
            {
                return ServiceResult<UserSummaryResponse>.Failure("validation_error", "El email indicado no es válido.");
            }

            if (await _userRepository.GetByEmailAsync(emailContact.NormalizedValue, cancellationToken) is not null)
            {
                return ServiceResult<UserSummaryResponse>.Failure("conflict", "Ya existe un usuario con ese email.");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            phoneContact = ParseContact(request.PhoneNumber);
            if (phoneContact is null || phoneContact.Type != ContactType.Phone)
            {
                return ServiceResult<UserSummaryResponse>.Failure("validation_error", "El teléfono indicado no es válido.");
            }

            if (await _userRepository.GetByPhoneAsync(phoneContact.NormalizedValue, cancellationToken) is not null)
            {
                return ServiceResult<UserSummaryResponse>.Failure("conflict", "Ya existe un usuario con ese número de teléfono.");
            }
        }

        DateTime now = DateTime.UtcNow;
        UserAccount user = new()
        {
            Id = Guid.NewGuid(),
            Email = emailContact?.OriginalValue,
            NormalizedEmail = emailContact?.NormalizedValue,
            UserName = request.UserName.Trim(),
            NormalizedUserName = normalizedUserName,
            FirstName = NormalizeNullable(request.FirstName),
            LastName = NormalizeNullable(request.LastName),
            DisplayName = BuildDisplayName(request.FirstName, request.LastName, request.UserName),
            PhoneNumber = phoneContact?.OriginalValue,
            NormalizedPhoneNumber = phoneContact?.NormalizedValue,
            EmailConfirmed = emailContact is not null,
            PhoneNumberConfirmed = phoneContact is not null,
            RegistrationCompleted = true,
            RegistrationInitiatedAtUtc = now,
            RegistrationCompletedAtUtc = now,
            Role = UserRole.User,
            Status = UserStatus.Active,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            LastSeenAtUtc = now
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        await _userRepository.AddAsync(user, cancellationToken);
        await _userAuthEventRepository.AddAsync(
            BuildAuthEvent(AuthEventType.ClassicRegisterCreated, user, user.Email ?? user.PhoneNumber ?? user.UserName, true, null, null, null, null, now),
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        string userCode = await _userCodeDirectoryService.EnsureUserCodeAsync(user.Id, cancellationToken);
        return ServiceResult<UserSummaryResponse>.Success(user.ToResponse(userCode));
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

        string normalizedIdentity = NormalizeIdentityForLookup(request.Identity);
        UserAccount? user = await _userRepository.GetByIdentityAsync(normalizedIdentity, cancellationToken);
        DateTime now = DateTime.UtcNow;

        if (user is null)
        {
            await PersistAnonymousEventAsync(AuthEventType.LoginFailed, request.Identity, "Usuario no encontrado.", ipAddress, userAgent, request.Client, now, cancellationToken);
            return ServiceResult<AuthResponse>.Failure("unauthorized", "Credenciales inválidas.");
        }

        if (!user.RegistrationCompleted || user.Status == UserStatus.PendingActivation)
        {
            return ServiceResult<AuthResponse>.Failure("unauthorized", "Debes completar el registro antes de iniciar sesión.");
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

            string userCode = await _userCodeDirectoryService.EnsureUserCodeAsync(user.Id, cancellationToken);
            return ServiceResult<AuthResponse>.Success(BuildAuthResponse(user, userCode, session, tokens));
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
                BuildAuthEvent(AuthEventType.RefreshFailed, session.User, session.User.Email ?? session.User.PhoneNumber, false, "Sesión expirada o revocada.", ipAddress, userAgent, request.Client, now),
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
                BuildAuthEvent(AuthEventType.RefreshSucceeded, session.User, session.User.Email ?? session.User.PhoneNumber, true, null, ipAddress, userAgent, request.Client, now),
                cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            string userCode = await _userCodeDirectoryService.EnsureUserCodeAsync(session.User.Id, cancellationToken);
            return ServiceResult<AuthResponse>.Success(BuildAuthResponse(session.User, userCode, session, tokens));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error refrescando sesión {SessionId}", session.Id);
            return ServiceResult<AuthResponse>.Failure("server_error", "No se pudo refrescar la sesión.");
        }
    }

    public async Task<ServiceResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return ServiceResult.Failure("validation_error", "Debes indicar la contraseña actual y la nueva.");
        }

        if (request.NewPassword != request.ConfirmNewPassword)
        {
            return ServiceResult.Failure("validation_error", "La confirmación de contraseña no coincide.");
        }

        UserAccount? user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return ServiceResult.Failure("not_found", "Usuario no encontrado.");
        }

        PasswordVerificationResult currentPasswordValidation = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
        if (currentPasswordValidation == PasswordVerificationResult.Failed)
        {
            return ServiceResult.Failure("unauthorized", "La contraseña actual no es correcta.");
        }

        DateTime now = DateTime.UtcNow;
        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        user.UpdatedAtUtc = now;
        _userRepository.Update(user);

        IReadOnlyCollection<UserSession> activeSessions = await _userSessionRepository.GetActiveByUserIdAsync(userId, cancellationToken);
        foreach (UserSession session in activeSessions)
        {
            session.RevokedAtUtc = now;
            session.RevocationReason = "PasswordChanged";
            session.LastSeenAtUtc = now;
            _userSessionRepository.Update(session);
        }

        await _userAuthEventRepository.AddAsync(
            BuildAuthEvent(AuthEventType.PasswordChanged, user, user.Email ?? user.PhoneNumber, true, null, null, null, null, now),
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success();
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
            BuildAuthEvent(AuthEventType.Logout, session.User, session.User?.Email ?? session.User?.PhoneNumber, true, null, session.IpAddress, session.UserAgent, null, now),
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

    private string BuildCompletionLink(string contact, string validationToken)
    {
        string encodedContact = Uri.EscapeDataString(contact);
        string encodedToken = Uri.EscapeDataString(validationToken);
        return $"{_userRegistrationOptions.CompletionUrlBase}?contact={encodedContact}&token={encodedToken}";
    }

    private static string BuildRegistrationEmailHtml(string userName, string temporaryPassword, string completionLink)
        => $"""
           <h2>Completa tu registro</h2>
           <p>Ya hemos preparado tu acceso inicial.</p>
           <p><strong>Usuario:</strong> {userName}</p>
           <p><strong>Contraseña temporal:</strong> {temporaryPassword}</p>
           <p>Completa el registro desde este enlace:</p>
           <p><a href="{completionLink}">{completionLink}</a></p>
           <p>Cuando termines el proceso, podrás iniciar sesión y cambiar tu contraseña.</p>
           """;

    private static string BuildRegistrationEmailText(string userName, string temporaryPassword, string completionLink)
        => $"""
           Completa tu registro.

           Usuario: {userName}
           Contraseña temporal: {temporaryPassword}

           Enlace para completar el registro:
           {completionLink}
           """;

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

    private static AuthResponse BuildAuthResponse(UserAccount user, string userCode, UserSession session, GeneratedTokenEnvelope tokens)
        => new()
        {
            AccessToken = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken,
            AccessTokenExpiresAtUtc = tokens.AccessTokenExpiresAtUtc,
            RefreshTokenExpiresAtUtc = tokens.RefreshTokenExpiresAtUtc,
            User = user.ToResponse(userCode),
            CurrentSession = session.ToResponse(session.Id)
        };

    private static ContactInfo? ParseContact(string contact)
    {
        string trimmedContact = contact.Trim();
        if (string.IsNullOrWhiteSpace(trimmedContact))
        {
            return null;
        }

        if (IsEmail(trimmedContact))
        {
            return new ContactInfo(ContactType.Email, trimmedContact, trimmedContact.ToUpperInvariant());
        }

        string normalizedPhone = NormalizePhone(trimmedContact);
        return normalizedPhone.Length >= 7
            ? new ContactInfo(ContactType.Phone, trimmedContact, normalizedPhone)
            : null;
    }

    private static bool IsEmail(string value)
    {
        try
        {
            MailAddress _ = new(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeIdentityForLookup(string value)
    {
        string trimmedValue = value.Trim();
        return IsEmail(trimmedValue)
            ? trimmedValue.ToUpperInvariant()
            : IsPhone(trimmedValue)
                ? NormalizePhone(trimmedValue)
                : trimmedValue.ToUpperInvariant();
    }

    private static bool IsPhone(string value)
        => NormalizePhone(value).Length >= 7;

    private static string NormalizePhone(string value)
        => new(value.Where(char.IsDigit).ToArray());

    private static string GenerateTemporaryPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@$?";
        byte[] bytes = RandomNumberGenerator.GetBytes(12);
        StringBuilder builder = new();

        foreach (byte @byte in bytes)
        {
            builder.Append(chars[@byte % chars.Length]);
        }

        return builder.ToString();
    }

    private static string BuildDisplayName(string? firstName, string? lastName, string userName)
    {
        string displayName = $"{firstName} {lastName}".Trim();
        return string.IsNullOrWhiteSpace(displayName) ? userName.Trim() : displayName;
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private enum ContactType
    {
        Email,
        Phone
    }

    private sealed record ContactInfo(ContactType Type, string OriginalValue, string NormalizedValue);
}
