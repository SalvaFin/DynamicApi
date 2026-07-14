using System.Net;
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
    private readonly IUserExternalLoginRepository _userExternalLoginRepository;
    private readonly IPasswordHasher<UserAccount> _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IExternalAuthTokenValidator _externalAuthTokenValidator;
    private readonly IEmailNotificationService _emailNotificationService;
    private readonly IRegistrationRewardService _registrationRewardService;
    private readonly IUserCodeDirectoryService _userCodeDirectoryService;
    private readonly UserRegistrationOptions _userRegistrationOptions;
    private readonly ExternalAuthOptions _externalAuthOptions;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        DynamicUsersDbContext dbContext,
        IUserRepository userRepository,
        IUserDeviceRepository userDeviceRepository,
        IUserSessionRepository userSessionRepository,
        IUserAuthEventRepository userAuthEventRepository,
        IUserExternalLoginRepository userExternalLoginRepository,
        IPasswordHasher<UserAccount> passwordHasher,
        IJwtTokenService jwtTokenService,
        IExternalAuthTokenValidator externalAuthTokenValidator,
        IEmailNotificationService emailNotificationService,
        IRegistrationRewardService registrationRewardService,
        IUserCodeDirectoryService userCodeDirectoryService,
        IOptions<UserRegistrationOptions> userRegistrationOptions,
        IOptions<ExternalAuthOptions> externalAuthOptions,
        ILogger<AuthService> logger)
    {
        _dbContext = dbContext;
        _userRepository = userRepository;
        _userDeviceRepository = userDeviceRepository;
        _userSessionRepository = userSessionRepository;
        _userAuthEventRepository = userAuthEventRepository;
        _userExternalLoginRepository = userExternalLoginRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _externalAuthTokenValidator = externalAuthTokenValidator;
        _emailNotificationService = emailNotificationService;
        _registrationRewardService = registrationRewardService;
        _userCodeDirectoryService = userCodeDirectoryService;
        _userRegistrationOptions = userRegistrationOptions.Value;
        _externalAuthOptions = externalAuthOptions.Value;
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
        user.PasswordIsTemporary = true;
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
                        HtmlBody = BuildDynamicRegistrationEmailHtml(user.UserName, completionLink),
                        TextBody = BuildDynamicRegistrationEmailText(user.UserName, completionLink)
                    },
                    cancellationToken);

                notificationSent = true;
                message = "Te hemos enviado un correo con el enlace seguro para completar el registro.";
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
        if (!request.TermsAccepted || !request.PrivacyPolicyAccepted)
        {
            return ServiceResult<CompleteRegistrationResponse>.Failure(
                "validation_error",
                "Debes aceptar los terminos y confirmar que has leido la politica de privacidad.");
        }

        if (string.IsNullOrWhiteSpace(request.Contact) ||
            string.IsNullOrWhiteSpace(request.ValidationToken) ||
            string.IsNullOrWhiteSpace(request.Nombre) ||
            string.IsNullOrWhiteSpace(request.Apellidos) ||
            !request.Province.HasValue ||
            !Enum.IsDefined(request.Province.Value))
        {
            return ServiceResult<CompleteRegistrationResponse>.Failure(
                "validation_error",
                "Contacto, token, nombre, apellidos y provincia son obligatorios.");
        }

        ServiceResult<int> birthDateValidation = ValidateBirthDateForRegistration(request.BirthDate);
        if (!birthDateValidation.Succeeded)
        {
            return ServiceResult<CompleteRegistrationResponse>.Failure(
                birthDateValidation.ErrorCode ?? "validation_error",
                birthDateValidation.ErrorMessage ?? $"La fecha de nacimiento debe indicar al menos {_userRegistrationOptions.MinimumAge} años.");
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
        userByContact.AgeAtRegistration = null;
        userByContact.BirthDate = request.BirthDate!.Value.Date;
        userByContact.Gender = request.Gender;
        userByContact.PostalCode = NormalizePostalCode(request.PostalCode);
        userByContact.Province = request.Province.Value;
        userByContact.RegistrationCompleted = true;
        userByContact.RegistrationCompletedAtUtc = now;
        userByContact.RegistrationValidationToken = null;
        userByContact.RegistrationValidationTokenExpiresAtUtc = null;
        userByContact.Status = UserStatus.Active;
        userByContact.LastLoginAtUtc = now;
        userByContact.LastSeenAtUtc = now;
        userByContact.LastLoginIp = ipAddress;
        userByContact.UpdatedAtUtc = now;
        ApplyRegistrationAcceptances(
            userByContact,
            request.TermsAccepted,
            request.PrivacyPolicyAccepted,
            request.MarketingAccepted,
            now);

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
        user.PasswordIsTemporary = false;

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

    public async Task<ServiceResult<PasswordResetStartResponse>> RequestPasswordResetAsync(
        ForgotPasswordRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        const string acceptedMessage = "Si existe una cuenta asociada a ese correo, enviaremos instrucciones para recuperar la contraseña.";

        if (string.IsNullOrWhiteSpace(request.Email) || !IsEmail(request.Email))
        {
            return ServiceResult<PasswordResetStartResponse>.Failure("validation_error", "Debes indicar un email válido.");
        }

        string email = request.Email.Trim();
        string normalizedEmail = email.ToUpperInvariant();
        UserAccount? user = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        DateTime now = DateTime.UtcNow;

        if (user is null || !user.RegistrationCompleted || string.IsNullOrWhiteSpace(user.Email))
        {
            await PersistAnonymousEventAsync(
                AuthEventType.PasswordResetRequested,
                email,
                user is null ? "Cuenta no encontrada." : "Cuenta sin registro completado.",
                ipAddress,
                userAgent,
                request.Client,
                now,
                cancellationToken);

            return ServiceResult<PasswordResetStartResponse>.Success(new PasswordResetStartResponse
            {
                RequestAccepted = true,
                Message = acceptedMessage
            });
        }

        string resetToken = GenerateUrlSafeToken();
        user.PasswordResetTokenHash = HashToken(resetToken);
        user.PasswordResetTokenExpiresAtUtc = now.AddHours(_userRegistrationOptions.PasswordResetTokenExpirationHours);
        user.PasswordResetRequestedAtUtc = now;
        user.UpdatedAtUtc = now;
        _userRepository.Update(user);

        await _userAuthEventRepository.AddAsync(
            BuildAuthEvent(AuthEventType.PasswordResetRequested, user, email, true, null, ipAddress, userAgent, request.Client, now),
            cancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);

            string resetLink = BuildPasswordResetLink(user.Email, resetToken);
            await _emailNotificationService.SendAsync(
                new EmailMessage
                {
                    ToEmail = user.Email,
                    ToName = user.DisplayName ?? user.UserName,
                    Subject = "Recupera tu contraseña en Dynamic",
                    HtmlBody = BuildDynamicPasswordResetEmailHtml(user.DisplayName ?? user.UserName, resetLink),
                    TextBody = BuildPasswordResetEmailText(user.DisplayName ?? user.UserName, resetLink)
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error solicitando recuperación de contraseña para {Email}", email);
            return ServiceResult<PasswordResetStartResponse>.Failure("server_error", "No se pudo enviar el correo de recuperación.");
        }

        return ServiceResult<PasswordResetStartResponse>.Success(new PasswordResetStartResponse
        {
            RequestAccepted = true,
            Message = acceptedMessage
        });
    }

    public async Task<ServiceResult<PasswordResetResponse>> ResetPasswordAsync(
        ResetPasswordRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Token) ||
            string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return ServiceResult<PasswordResetResponse>.Failure("validation_error", "Email, token y nueva contraseña son obligatorios.");
        }

        if (!IsEmail(request.Email))
        {
            return ServiceResult<PasswordResetResponse>.Failure("validation_error", "El email indicado no es válido.");
        }

        if (request.NewPassword != request.ConfirmNewPassword)
        {
            return ServiceResult<PasswordResetResponse>.Failure("validation_error", "La confirmación de contraseña no coincide.");
        }

        if (request.NewPassword.Length < 8)
        {
            return ServiceResult<PasswordResetResponse>.Failure("validation_error", "La nueva contraseña debe tener al menos 8 caracteres.");
        }

        string email = request.Email.Trim();
        UserAccount? user = await _userRepository.GetByEmailAsync(email.ToUpperInvariant(), cancellationToken);
        string tokenHash = HashToken(request.Token.Trim());
        DateTime now = DateTime.UtcNow;

        if (user is null ||
            string.IsNullOrWhiteSpace(user.PasswordResetTokenHash) ||
            !string.Equals(user.PasswordResetTokenHash, tokenHash, StringComparison.Ordinal) ||
            !user.PasswordResetTokenExpiresAtUtc.HasValue ||
            user.PasswordResetTokenExpiresAtUtc.Value < now)
        {
            if (user is not null)
            {
                await _userAuthEventRepository.AddAsync(
                    BuildAuthEvent(AuthEventType.PasswordResetCompleted, user, email, false, "Token de recuperación inválido o expirado.", ipAddress, userAgent, request.Client, now),
                    cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return ServiceResult<PasswordResetResponse>.Failure("unauthorized", "El enlace de recuperación no es válido o ha expirado.");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
            user.PasswordIsTemporary = false;
            user.PasswordResetTokenHash = null;
            user.PasswordResetTokenExpiresAtUtc = null;
            user.PasswordResetRequestedAtUtc = null;
            user.FailedLoginCount = 0;
            user.LockedUntilUtc = null;
            user.UpdatedAtUtc = now;
            _userRepository.Update(user);

            IReadOnlyCollection<UserSession> activeSessions = await _userSessionRepository.GetActiveByUserIdAsync(user.Id, cancellationToken);
            foreach (UserSession session in activeSessions)
            {
                session.RevokedAtUtc = now;
                session.RevocationReason = "PasswordReset";
                session.LastSeenAtUtc = now;
                _userSessionRepository.Update(session);
            }

            await _userAuthEventRepository.AddAsync(
                BuildAuthEvent(AuthEventType.PasswordResetCompleted, user, email, true, null, ipAddress, userAgent, request.Client, now),
                cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ServiceResult<PasswordResetResponse>.Success(new PasswordResetResponse
            {
                PasswordChanged = true,
                Message = "Contraseña actualizada correctamente. Inicia sesión con tu nueva contraseña."
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error restableciendo contraseña para {Email}", email);
            return ServiceResult<PasswordResetResponse>.Failure("server_error", "No se pudo restablecer la contraseña.");
        }
    }

    public async Task<ServiceResult<AuthResponse>> ExternalLoginAsync(
        ExternalLoginRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseExternalProvider(request.Provider, out ExternalAuthProvider provider) ||
            string.IsNullOrWhiteSpace(request.IdToken))
        {
            return ServiceResult<AuthResponse>.Failure("validation_error", "Proveedor e id_token son obligatorios.");
        }

        DateTime now = DateTime.UtcNow;
        ExternalAuthTokenPayload? externalPayload = await _externalAuthTokenValidator.ValidateAsync(
            provider,
            request.IdToken,
            request.Nonce,
            cancellationToken);

        if (externalPayload is null || string.IsNullOrWhiteSpace(externalPayload.Subject))
        {
            await PersistAnonymousEventAsync(
                AuthEventType.ExternalLoginFailed,
                request.Provider,
                "Token externo invalido.",
                ipAddress,
                userAgent,
                request.Client,
                now,
                cancellationToken);

            return ServiceResult<AuthResponse>.Failure("unauthorized", "No se pudo validar el login externo.");
        }

        if (!string.IsNullOrWhiteSpace(request.QrToken))
        {
            bool isQrTokenValid = await _registrationRewardService.ValidateQrTokenAsync(request.QrToken, cancellationToken);
            if (!isQrTokenValid)
            {
                return ServiceResult<AuthResponse>.Failure("validation_error", "El QR de registro no es valido o ya no esta disponible.");
            }
        }

        ApplyExternalProfileHints(externalPayload, request);
        string identity = BuildExternalIdentity(provider, externalPayload);
        UserExternalLogin? externalLogin = await _userExternalLoginRepository.GetByProviderAsync(
            provider,
            externalPayload.Subject,
            cancellationToken);

        UserAccount? user = externalLogin?.User;
        bool linkedExternalLogin = false;

        if (user is null &&
            _externalAuthOptions.LinkExistingUsersByVerifiedEmail &&
            CanLinkExistingUserByEmail(externalPayload) &&
            externalPayload.Email is not null)
        {
            user = await _userRepository.GetByEmailAsync(externalPayload.Email.ToUpperInvariant(), cancellationToken);
        }

        if (user is not null)
        {
            if (user.Status is UserStatus.Disabled or UserStatus.Deleted)
            {
                return ServiceResult<AuthResponse>.Failure("unauthorized", "La cuenta no esta disponible.");
            }

            if (user.LockedUntilUtc.HasValue && user.LockedUntilUtc.Value > now)
            {
                return ServiceResult<AuthResponse>.Failure("locked", "La cuenta esta temporalmente bloqueada por seguridad.");
            }
        }

        if (user is null || !user.RegistrationCompleted)
        {
            return ServiceResult<AuthResponse>.Failure(
                "external_registration_required",
                "Debes completar tu perfil y las condiciones de Dynamic antes de continuar.");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            CompleteOrUpdateExternalUser(user, externalPayload, ipAddress, now);
            _userRepository.Update(user);

            if (externalLogin is null)
            {
                externalLogin = new UserExternalLogin
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    User = user,
                    Provider = provider,
                    ProviderSubject = externalPayload.Subject,
                    Email = externalPayload.Email,
                    DisplayName = externalPayload.DisplayName,
                    CreatedAtUtc = now,
                    LastLoginAtUtc = now
                };

                await _userExternalLoginRepository.AddAsync(externalLogin, cancellationToken);
                linkedExternalLogin = true;
            }
            else
            {
                externalLogin.Email = externalPayload.Email ?? externalLogin.Email;
                externalLogin.DisplayName = externalPayload.DisplayName ?? externalLogin.DisplayName;
                externalLogin.LastLoginAtUtc = now;
                _userExternalLoginRepository.Update(externalLogin);
            }

            UserDevice? device = await CreateOrUpdateDeviceAsync(user, request.Client, now, cancellationToken);
            UserSession session = CreateSession(user, device, request.Client, ipAddress, userAgent, now);
            GeneratedTokenEnvelope tokens = _jwtTokenService.GenerateTokens(user, session);

            session.JwtId = tokens.JwtId;
            session.RefreshTokenHash = tokens.RefreshTokenHash;
            session.RefreshTokenExpiresAtUtc = tokens.RefreshTokenExpiresAtUtc;

            await _userSessionRepository.AddAsync(session, cancellationToken);
            await _userAuthEventRepository.AddAsync(
                BuildAuthEvent(AuthEventType.ExternalLoginSucceeded, user, identity, true, linkedExternalLogin ? "ExternalLoginLinked" : null, ipAddress, userAgent, request.Client, now),
                cancellationToken);
            await _userAuthEventRepository.AddAsync(
                BuildAuthEvent(AuthEventType.LoginSucceeded, user, identity, true, null, ipAddress, userAgent, request.Client, now),
                cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            string userCode = await _userCodeDirectoryService.EnsureUserCodeAsync(user.Id, cancellationToken);
            return ServiceResult<AuthResponse>.Success(BuildAuthResponse(user, userCode, session, tokens));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error iniciando sesion externa con {Provider}", provider);
            return ServiceResult<AuthResponse>.Failure("server_error", "No se pudo iniciar sesion con el proveedor externo.");
        }
    }

    public async Task<ServiceResult<AuthResponse>> CompleteExternalRegistrationAsync(
        CompleteExternalRegistrationRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseExternalProvider(request.Provider, out ExternalAuthProvider provider) ||
            string.IsNullOrWhiteSpace(request.IdToken) ||
            string.IsNullOrWhiteSpace(request.Nombre) ||
            string.IsNullOrWhiteSpace(request.Apellidos) ||
            !request.Province.HasValue ||
            !Enum.IsDefined(request.Province.Value))
        {
            return ServiceResult<AuthResponse>.Failure(
                "validation_error",
                "Proveedor, id_token, nombre, apellidos y provincia son obligatorios.");
        }

        ServiceResult<int> birthDateValidation = ValidateBirthDateForRegistration(request.BirthDate);
        if (!birthDateValidation.Succeeded)
        {
            return ServiceResult<AuthResponse>.Failure(
                birthDateValidation.ErrorCode ?? "validation_error",
                birthDateValidation.ErrorMessage ?? $"La fecha de nacimiento debe indicar al menos {_userRegistrationOptions.MinimumAge} años.");
        }

        if (!request.TermsAccepted || !request.PrivacyPolicyAccepted)
        {
            return ServiceResult<AuthResponse>.Failure(
                "validation_error",
                "Debes aceptar los terminos y confirmar que has leido la politica de privacidad.");
        }

        DateTime now = DateTime.UtcNow;
        ExternalAuthTokenPayload? externalPayload = await _externalAuthTokenValidator.ValidateAsync(
            provider,
            request.IdToken,
            request.Nonce,
            cancellationToken);

        if (externalPayload is null || string.IsNullOrWhiteSpace(externalPayload.Subject))
        {
            await PersistAnonymousEventAsync(
                AuthEventType.ExternalLoginFailed,
                request.Provider,
                "Token externo invalido durante el registro.",
                ipAddress,
                userAgent,
                request.Client,
                now,
                cancellationToken);

            return ServiceResult<AuthResponse>.Failure("unauthorized", "No se pudo validar el registro externo.");
        }

        if (!string.IsNullOrWhiteSpace(request.QrToken))
        {
            bool isQrTokenValid = await _registrationRewardService.ValidateQrTokenAsync(request.QrToken, cancellationToken);
            if (!isQrTokenValid)
            {
                return ServiceResult<AuthResponse>.Failure(
                    "validation_error",
                    "El QR de registro no es valido o ya no esta disponible.");
            }
        }

        string identity = BuildExternalIdentity(provider, externalPayload);
        UserExternalLogin? externalLogin = await _userExternalLoginRepository.GetByProviderAsync(
            provider,
            externalPayload.Subject,
            cancellationToken);

        UserAccount? user = externalLogin?.User;
        if (user is null &&
            _externalAuthOptions.LinkExistingUsersByVerifiedEmail &&
            CanLinkExistingUserByEmail(externalPayload) &&
            externalPayload.Email is not null)
        {
            user = await _userRepository.GetByEmailAsync(externalPayload.Email.ToUpperInvariant(), cancellationToken);
        }

        if (user?.RegistrationCompleted == true)
        {
            return ServiceResult<AuthResponse>.Failure(
                "conflict",
                "La cuenta externa ya esta registrada. Inicia sesion con el proveedor.");
        }

        if (user is not null && user.Status is UserStatus.Disabled or UserStatus.Deleted)
        {
            return ServiceResult<AuthResponse>.Failure("unauthorized", "La cuenta no esta disponible.");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            if (user is null)
            {
                user = await CreateExternalUserAsync(externalPayload, now, cancellationToken);
            }
            else
            {
                CompleteOrUpdateExternalUser(user, externalPayload, ipAddress, now);
                _userRepository.Update(user);
            }

            user.FirstName = request.Nombre.Trim();
            user.LastName = request.Apellidos.Trim();
            user.DisplayName = $"{user.FirstName} {user.LastName}".Trim();
            user.AgeAtRegistration = null;
            user.BirthDate = request.BirthDate!.Value.Date;
            user.Gender = request.Gender;
            user.PostalCode = NormalizePostalCode(request.PostalCode);
            user.Province = request.Province.Value;
            user.RegistrationCompleted = true;
            user.RegistrationCompletedAtUtc = now;
            user.Status = UserStatus.Active;
            user.LastLoginAtUtc = now;
            user.LastSeenAtUtc = now;
            user.LastLoginIp = ipAddress;
            user.UpdatedAtUtc = now;
            ApplyRegistrationAcceptances(
                user,
                request.TermsAccepted,
                request.PrivacyPolicyAccepted,
                request.MarketingAccepted,
                now);

            if (externalLogin is null)
            {
                externalLogin = new UserExternalLogin
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    User = user,
                    Provider = provider,
                    ProviderSubject = externalPayload.Subject,
                    Email = externalPayload.Email,
                    DisplayName = user.DisplayName,
                    CreatedAtUtc = now,
                    LastLoginAtUtc = now
                };

                await _userExternalLoginRepository.AddAsync(externalLogin, cancellationToken);
            }
            else
            {
                externalLogin.Email = externalPayload.Email ?? externalLogin.Email;
                externalLogin.DisplayName = user.DisplayName;
                externalLogin.LastLoginAtUtc = now;
                _userExternalLoginRepository.Update(externalLogin);
            }

            UserDevice? device = await CreateOrUpdateDeviceAsync(user, request.Client, now, cancellationToken);
            UserSession session = CreateSession(user, device, request.Client, ipAddress, userAgent, now);
            GeneratedTokenEnvelope tokens = _jwtTokenService.GenerateTokens(user, session);

            session.JwtId = tokens.JwtId;
            session.RefreshTokenHash = tokens.RefreshTokenHash;
            session.RefreshTokenExpiresAtUtc = tokens.RefreshTokenExpiresAtUtc;

            await _userSessionRepository.AddAsync(session, cancellationToken);
            await _userAuthEventRepository.AddAsync(
                BuildAuthEvent(AuthEventType.RegisterCompleted, user, identity, true, "ExternalRegistration", ipAddress, userAgent, request.Client, now),
                cancellationToken);
            await _userAuthEventRepository.AddAsync(
                BuildAuthEvent(AuthEventType.ExternalLoginSucceeded, user, identity, true, "ExternalRegistration", ipAddress, userAgent, request.Client, now),
                cancellationToken);
            await _userAuthEventRepository.AddAsync(
                BuildAuthEvent(AuthEventType.LoginSucceeded, user, identity, true, null, ipAddress, userAgent, request.Client, now),
                cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(request.QrToken))
            {
                try
                {
                    await _registrationRewardService.PreparePendingAssignmentAsync(user.Id, request.QrToken, cancellationToken);
                    await _registrationRewardService.FinalizePendingAssignmentsAsync(user.Id, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "No se pudo aplicar la recompensa de registro social para {UserId}", user.Id);
                }
            }

            string userCode = await _userCodeDirectoryService.EnsureUserCodeAsync(user.Id, cancellationToken);
            return ServiceResult<AuthResponse>.Success(BuildAuthResponse(user, userCode, session, tokens));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error completando registro externo con {Provider}", provider);
            return ServiceResult<AuthResponse>.Failure("server_error", "No se pudo completar el registro externo.");
        }
    }

    public async Task<ServiceResult<SetInitialPasswordResponse>> SetInitialPasswordAsync(
        Guid userId,
        SetInitialPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return ServiceResult<SetInitialPasswordResponse>.Failure("validation_error", "Debes indicar la nueva contraseña.");
        }

        if (request.NewPassword != request.ConfirmNewPassword)
        {
            return ServiceResult<SetInitialPasswordResponse>.Failure("validation_error", "La confirmación de contraseña no coincide.");
        }

        if (request.NewPassword.Length < 8)
        {
            return ServiceResult<SetInitialPasswordResponse>.Failure("validation_error", "La nueva contraseña debe tener al menos 8 caracteres.");
        }

        UserAccount? user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return ServiceResult<SetInitialPasswordResponse>.Failure("not_found", "Usuario no encontrado.");
        }

        if (!user.PasswordIsTemporary)
        {
            return ServiceResult<SetInitialPasswordResponse>.Failure("validation_error", "La cuenta no tiene una contraseña temporal pendiente de cambio.");
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        user.PasswordIsTemporary = false;
        user.UpdatedAtUtc = DateTime.UtcNow;
        _userRepository.Update(user);

        await _userAuthEventRepository.AddAsync(
            BuildAuthEvent(AuthEventType.PasswordChanged, user, user.Email ?? user.PhoneNumber, true, "InitialPasswordSet", null, null, null, user.UpdatedAtUtc),
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<SetInitialPasswordResponse>.Success(new SetInitialPasswordResponse
        {
            PasswordChanged = true,
            RequiresPasswordChange = false,
            Message = "Contraseña creada correctamente."
        });
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
        user.PasswordIsTemporary = false;
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

    private async Task<UserAccount> CreateExternalUserAsync(
        ExternalAuthTokenPayload externalPayload,
        DateTime now,
        CancellationToken cancellationToken)
    {
        string userName = await GenerateUniqueExternalUserNameAsync(externalPayload, cancellationToken);
        UserAccount user = new()
        {
            Id = Guid.NewGuid(),
            Email = externalPayload.Email,
            NormalizedEmail = externalPayload.Email?.ToUpperInvariant(),
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            FirstName = externalPayload.FirstName,
            LastName = externalPayload.LastName,
            DisplayName = BuildDisplayName(externalPayload.FirstName, externalPayload.LastName, externalPayload.DisplayName ?? userName),
            AvatarUrl = externalPayload.AvatarUrl,
            EmailConfirmed = externalPayload.EmailVerified && !string.IsNullOrWhiteSpace(externalPayload.Email),
            PhoneNumberConfirmed = false,
            RegistrationCompleted = true,
            RegistrationInitiatedAtUtc = now,
            RegistrationCompletedAtUtc = now,
            Role = UserRole.User,
            Status = UserStatus.Active,
            FailedLoginCount = 0,
            LockedUntilUtc = null,
            LastLoginAtUtc = now,
            LastSeenAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, GenerateTemporaryPassword());
        user.PasswordIsTemporary = false;

        await _userRepository.AddAsync(user, cancellationToken);
        return user;
    }

    private void CompleteOrUpdateExternalUser(
        UserAccount user,
        ExternalAuthTokenPayload externalPayload,
        string? ipAddress,
        DateTime now)
    {
        if (!string.IsNullOrWhiteSpace(externalPayload.Email) && string.IsNullOrWhiteSpace(user.Email))
        {
            user.Email = externalPayload.Email;
            user.NormalizedEmail = externalPayload.Email.ToUpperInvariant();
        }

        if (externalPayload.EmailVerified &&
            !string.IsNullOrWhiteSpace(externalPayload.Email) &&
            string.Equals(user.NormalizedEmail, externalPayload.Email.ToUpperInvariant(), StringComparison.Ordinal))
        {
            user.EmailConfirmed = true;
        }

        user.FirstName ??= externalPayload.FirstName;
        user.LastName ??= externalPayload.LastName;
        user.DisplayName ??= externalPayload.DisplayName ?? BuildDisplayName(user.FirstName, user.LastName, user.UserName);
        user.AvatarUrl ??= externalPayload.AvatarUrl;
        user.RegistrationCompleted = true;
        user.RegistrationCompletedAtUtc ??= now;
        user.RegistrationValidationToken = null;
        user.RegistrationValidationTokenExpiresAtUtc = null;
        user.Status = UserStatus.Active;
        user.FailedLoginCount = 0;
        user.LockedUntilUtc = null;
        user.LastLoginAtUtc = now;
        user.LastSeenAtUtc = now;
        user.LastLoginIp = ipAddress;
        user.UpdatedAtUtc = now;
    }

    private async Task<string> GenerateUniqueExternalUserNameAsync(
        ExternalAuthTokenPayload externalPayload,
        CancellationToken cancellationToken)
    {
        string seed = !string.IsNullOrWhiteSpace(externalPayload.Email) && externalPayload.Email.Contains('@')
            ? externalPayload.Email[..externalPayload.Email.IndexOf('@')]
            : $"{externalPayload.Provider}{externalPayload.Subject}";

        string baseUserName = NormalizeUserNameSeed(seed);
        if (baseUserName.Length == 0)
        {
            baseUserName = $"user{Guid.NewGuid():N}"[..16];
        }

        string candidate = baseUserName.Length > 48 ? baseUserName[..48] : baseUserName;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            string currentCandidate = attempt == 0
                ? candidate
                : $"{candidate}{RandomNumberGenerator.GetInt32(1000, 9999)}";

            if (await _userRepository.GetByUserNameAsync(currentCandidate.ToUpperInvariant(), cancellationToken) is null)
            {
                return currentCandidate;
            }
        }

        return $"user{Guid.NewGuid():N}"[..16];
    }

    private static void ApplyRegistrationAcceptances(
        UserAccount user,
        bool termsAccepted,
        bool privacyPolicyAccepted,
        bool marketingAccepted,
        DateTime now)
    {
        user.TermsAccepted = termsAccepted;
        user.TermsAcceptedAtUtc = termsAccepted ? now : null;
        user.PrivacyPolicyAccepted = privacyPolicyAccepted;
        user.PrivacyPolicyAcceptedAtUtc = privacyPolicyAccepted ? now : null;
        user.MarketingAccepted = marketingAccepted;
        user.MarketingAcceptedAtUtc = marketingAccepted ? now : null;
    }

    private ServiceResult<int> ValidateBirthDateForRegistration(DateTime? birthDate)
    {
        if (!birthDate.HasValue)
        {
            return ServiceResult<int>.Failure("validation_error", "La fecha de nacimiento es obligatoria.");
        }

        DateTime birthDateValue = birthDate.Value.Date;
        DateTime today = DateTime.UtcNow.Date;

        if (birthDateValue > today)
        {
            return ServiceResult<int>.Failure("validation_error", "La fecha de nacimiento no puede ser futura.");
        }

        int age = CalculateAge(birthDateValue, today);
        if (age < _userRegistrationOptions.MinimumAge)
        {
            return ServiceResult<int>.Failure(
                "validation_error",
                $"La edad mínima para registrarse es de {_userRegistrationOptions.MinimumAge} años.");
        }

        if (age > 130)
        {
            return ServiceResult<int>.Failure("validation_error", "La fecha de nacimiento no es válida.");
        }

        return ServiceResult<int>.Success(age);
    }

    private static int CalculateAge(DateTime birthDate, DateTime today)
    {
        int age = today.Year - birthDate.Year;
        if (birthDate.Date > today.AddYears(-age))
        {
            age--;
        }

        return age;
    }

    private static void ApplyExternalProfileHints(ExternalAuthTokenPayload externalPayload, ExternalLoginRequest request)
    {
        externalPayload.FirstName = NormalizeNullable(request.FirstName) ?? externalPayload.FirstName;
        externalPayload.LastName = NormalizeNullable(request.LastName) ?? externalPayload.LastName;
        externalPayload.DisplayName = NormalizeNullable(request.DisplayName) ?? externalPayload.DisplayName;
    }

    private static bool TryParseExternalProvider(string value, out ExternalAuthProvider provider)
        => Enum.TryParse(value.Trim(), ignoreCase: true, out provider) &&
           Enum.IsDefined(typeof(ExternalAuthProvider), provider);

    private static bool CanLinkExistingUserByEmail(ExternalAuthTokenPayload externalPayload)
    {
        if (!externalPayload.EmailVerified || string.IsNullOrWhiteSpace(externalPayload.Email))
        {
            return false;
        }

        return externalPayload.Provider == ExternalAuthProvider.Apple ||
               externalPayload.Email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase) ||
               !string.IsNullOrWhiteSpace(externalPayload.HostedDomain);
    }

    private static string BuildExternalIdentity(ExternalAuthProvider provider, ExternalAuthTokenPayload externalPayload)
        => $"{provider}:{externalPayload.Email ?? externalPayload.Subject}";

    private static string NormalizeUserNameSeed(string value)
    {
        StringBuilder builder = new();

        foreach (char character in value.Trim())
        {
            if (char.IsLetterOrDigit(character) || character is '_' or '-' or '.')
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
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

    private string BuildPasswordResetLink(string email, string resetToken)
    {
        string encodedEmail = Uri.EscapeDataString(email);
        string encodedToken = Uri.EscapeDataString(resetToken);
        return $"{_userRegistrationOptions.PasswordResetUrlBase}?email={encodedEmail}&token={encodedToken}";
    }

    private static string BuildDynamicRegistrationEmailHtml(string userName, string completionLink)
    {
        string safeUserName = WebUtility.HtmlEncode(userName);

        return BuildDynamicEmailHtml(
            preheader: "Tu acceso a Dynamic ya esta preparado.",
            eyebrow: "BIENVENIDO A DYNAMIC",
            title: "Completa tu registro",
            subtitle: "Ya hemos preparado tu cuenta. Termina el registro y entra directamente en Dynamic.",
            bodyHtml: $"""
                      <p style="margin:0 0 16px 0;color:#c9b8df;font-size:16px;line-height:24px;">Tu usuario inicial es:</p>
                      <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;margin:0 0 20px 0;">
                        <tr>
                          <td style="padding:14px 16px;border:1px solid #6f2dbd;border-radius:12px;background:#14081f;">
                            <div style="color:#9d5cff;font-size:12px;font-weight:700;letter-spacing:1.4px;text-transform:uppercase;margin-bottom:6px;">Usuario</div>
                            <div style="color:#ffffff;font-size:18px;font-weight:700;">{safeUserName}</div>
                          </td>
                        </tr>
                      </table>
                      <p style="margin:0;color:#a999bd;font-size:14px;line-height:22px;">Por seguridad no enviamos contrasenas por email. Al completar el formulario iniciaremos tu sesion y la app te pedira crear tu contrasena.</p>
                      """,
            ctaText: "Completar registro",
            ctaLink: completionLink,
            footerNote: "Si no has solicitado esta cuenta, puedes ignorar este correo.");
    }

    private static string BuildDynamicRegistrationEmailText(string userName, string completionLink)
        => $"""
           Completa tu registro en Dynamic.

           Usuario: {userName}

           Por seguridad no enviamos contrasenas por email.
           Completa el formulario desde este enlace y la app iniciara tu sesion:
           {completionLink}

           Despues podras crear tu contrasena desde la app.
           """;

    private static string BuildDynamicPasswordResetEmailHtml(string displayName, string resetLink)
    {
        string safeDisplayName = WebUtility.HtmlEncode(displayName);

        return BuildDynamicEmailHtml(
            preheader: "Recupera tu contrasena de Dynamic.",
            eyebrow: "SEGURIDAD DYNAMIC",
            title: "Recupera tu contrasena",
            subtitle: $"Hola {safeDisplayName}, hemos recibido una solicitud para cambiar tu contrasena.",
            bodyHtml: """
                      <p style="margin:0;color:#c9b8df;font-size:16px;line-height:24px;">Pulsa el boton para crear una nueva contrasena. Por seguridad, el enlace caduca pronto y solo puede usarse una vez.</p>
                      """,
            ctaText: "Crear nueva contrasena",
            ctaLink: resetLink,
            footerNote: "Si no has solicitado este cambio, puedes ignorar este correo.");
    }

    private static string BuildDynamicEmailHtml(
        string preheader,
        string eyebrow,
        string title,
        string subtitle,
        string bodyHtml,
        string ctaText,
        string ctaLink,
        string footerNote)
    {
        string safePreheader = WebUtility.HtmlEncode(preheader);
        string safeEyebrow = WebUtility.HtmlEncode(eyebrow);
        string safeTitle = WebUtility.HtmlEncode(title);
        string safeSubtitle = subtitle;
        string safeCtaText = WebUtility.HtmlEncode(ctaText);
        string safeCtaLink = WebUtility.HtmlEncode(ctaLink);
        string safeFooterNote = WebUtility.HtmlEncode(footerNote);

        return $"""
           <!doctype html>
           <html lang="es">
           <head>
             <meta charset="utf-8">
             <meta name="viewport" content="width=device-width, initial-scale=1">
             <meta name="color-scheme" content="dark">
             <meta name="supported-color-schemes" content="dark">
             <title>{safeTitle}</title>
           </head>
           <body style="margin:0;padding:0;background:#050307;color:#ffffff;font-family:Inter,Segoe UI,Roboto,Arial,sans-serif;">
             <div style="display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;">{safePreheader}</div>
             <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;background:#050307;">
               <tr>
                 <td align="center" style="padding:38px 16px;">
                   <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;max-width:640px;">
                     <tr>
                       <td style="padding:0 0 22px 0;">
                         <div style="font-size:24px;font-weight:800;letter-spacing:-0.4px;color:#9d5cff;text-shadow:0 0 18px #8d35ff;">Dynamic</div>
                       </td>
                     </tr>
                     <tr>
                       <td style="border:1px solid #5f268f;border-radius:22px;background:#0d0713;background-image:linear-gradient(135deg,#170820 0%,#09060d 54%,#13051f 100%);box-shadow:0 0 34px rgba(157,92,255,0.28);padding:0;overflow:hidden;">
                         <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;">
                           <tr>
                             <td style="padding:36px 34px 8px 34px;">
                               <div style="color:#c27cff;font-size:12px;font-weight:800;letter-spacing:3px;text-transform:uppercase;margin:0 0 12px 0;">{safeEyebrow}</div>
                               <h1 style="margin:0;color:#ffffff;font-size:34px;line-height:40px;font-weight:900;letter-spacing:-0.8px;text-shadow:0 0 20px rgba(177,107,255,0.85);">{safeTitle}</h1>
                               <p style="margin:16px 0 0 0;color:#c9b8df;font-size:17px;line-height:26px;">{safeSubtitle}</p>
                             </td>
                           </tr>
                           <tr>
                             <td style="padding:24px 34px 0 34px;">
                               <div style="border:1px solid #5b238c;border-radius:18px;background:#100718;padding:22px;box-shadow:inset 0 0 28px rgba(111,45,189,0.18);">
                                 {bodyHtml}
                               </div>
                             </td>
                           </tr>
                           <tr>
                             <td align="center" style="padding:30px 34px 10px 34px;">
                               <a href="{safeCtaLink}" style="display:inline-block;background:#9d5cff;background-image:linear-gradient(90deg,#7c3aed,#c084fc);color:#ffffff;text-decoration:none;font-weight:900;font-size:16px;line-height:20px;padding:16px 28px;border-radius:999px;box-shadow:0 0 24px rgba(157,92,255,0.75);">{safeCtaText}</a>
                             </td>
                           </tr>
                           <tr>
                             <td style="padding:14px 34px 32px 34px;">
                               <p style="margin:0 0 12px 0;color:#7f6c92;font-size:12px;line-height:18px;text-align:center;">Si el boton no funciona, copia y pega este enlace:</p>
                               <p style="margin:0;color:#b98cff;font-size:12px;line-height:18px;word-break:break-all;text-align:center;"><a href="{safeCtaLink}" style="color:#b98cff;text-decoration:underline;">{safeCtaLink}</a></p>
                             </td>
                           </tr>
                         </table>
                       </td>
                     </tr>
                     <tr>
                       <td style="padding:22px 10px 0 10px;">
                         <p style="margin:0;color:#7f6c92;font-size:12px;line-height:18px;text-align:center;">{safeFooterNote}</p>
                       </td>
                     </tr>
                   </table>
                 </td>
               </tr>
             </table>
           </body>
           </html>
           """;
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

    private static string BuildPasswordResetEmailHtml(string displayName, string resetLink)
        => $"""
           <h2>Recupera tu contraseña</h2>
           <p>Hola {displayName}, hemos recibido una solicitud para cambiar tu contraseña.</p>
           <p>Usa este enlace para crear una nueva contraseña:</p>
           <p><a href="{resetLink}">{resetLink}</a></p>
           <p>Si no has solicitado este cambio, puedes ignorar este correo.</p>
           """;

    private static string BuildPasswordResetEmailText(string displayName, string resetLink)
        => $"""
           Recupera tu contraseña.

           Hola {displayName}, hemos recibido una solicitud para cambiar tu contraseña.

           Enlace para crear una nueva contraseña:
           {resetLink}

           Si no has solicitado este cambio, puedes ignorar este correo.
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

    private static string GenerateUrlSafeToken()
    {
        string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return token.Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static string HashToken(string token)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string BuildDisplayName(string? firstName, string? lastName, string userName)
    {
        string displayName = $"{firstName} {lastName}".Trim();
        return string.IsNullOrWhiteSpace(displayName) ? userName.Trim() : displayName;
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizePostalCode(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private enum ContactType
    {
        Email,
        Phone
    }

    private sealed record ContactInfo(ContactType Type, string OriginalValue, string NormalizedValue);
}
