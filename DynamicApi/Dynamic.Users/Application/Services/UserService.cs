using System.Net.Mail;
using Dynamic.Negocios.Application.Contracts.Repositories;
using Dynamic.Negocios.Domain.Entities;
using Dynamic.Negocios.Domain.Enums;
using Dynamic.Users.Application.Common;
using Dynamic.Users.Application.Contracts.Repositories;
using Dynamic.Users.Application.Contracts.Services;
using Dynamic.Users.Application.DTOs.Requests;
using Dynamic.Users.Application.DTOs.Responses;
using Dynamic.Users.Application.Mappings;
using Dynamic.Users.Domain.Entities;
using Dynamic.Users.Domain.Enums;
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
    private readonly INegocioUsuarioVinculacionRepository _negocioUsuarioVinculacionRepository;

    public UserService(
        DynamicUsersDbContext dbContext,
        IUserRepository userRepository,
        IUserSessionRepository userSessionRepository,
        IUserDeviceRepository userDeviceRepository,
        IUserCodeDirectoryService userCodeDirectoryService,
        INegocioUsuarioVinculacionRepository negocioUsuarioVinculacionRepository)
    {
        _dbContext = dbContext;
        _userRepository = userRepository;
        _userSessionRepository = userSessionRepository;
        _userDeviceRepository = userDeviceRepository;
        _userCodeDirectoryService = userCodeDirectoryService;
        _negocioUsuarioVinculacionRepository = negocioUsuarioVinculacionRepository;
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

    public async Task<ServiceResult<BusinessCustomerLookupResponse>> SearchBusinessCustomerByContactAsync(
        Guid requesterUserId,
        bool isAdmin,
        BusinessCustomerSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ServiceResult authorization = await EnsureCanSearchCustomersAsync(requesterUserId, isAdmin, cancellationToken);
        if (!authorization.Succeeded)
        {
            return ServiceResult<BusinessCustomerLookupResponse>.Failure(
                authorization.ErrorCode ?? "forbidden",
                authorization.ErrorMessage ?? "Sin permisos.");
        }

        ContactInfo? contact = ParseContact(request.Contact);
        if (contact is null)
        {
            return ServiceResult<BusinessCustomerLookupResponse>.Failure(
                "validation_error",
                "Introduce un email valido o un numero de telefono de al menos 7 digitos.");
        }

        UserAccount? user = contact.Type == ContactType.Email
            ? await _userRepository.GetByEmailAsync(contact.NormalizedValue, cancellationToken)
            : await _userRepository.GetByPhoneAsync(contact.NormalizedValue, cancellationToken);

        if (user is null || !IsSearchableCustomer(user))
        {
            return ServiceResult<BusinessCustomerLookupResponse>.Failure("not_found", "No se ha encontrado ningun usuario cliente con ese contacto.");
        }

        return ServiceResult<BusinessCustomerLookupResponse>.Success(await BuildCustomerLookupResponseAsync(user, contact.Type.ToString(), cancellationToken));
    }

    public async Task<ServiceResult<BusinessCustomerLookupResponse>> GetBusinessCustomerByIdAsync(
        Guid requesterUserId,
        bool isAdmin,
        Guid customerUserId,
        CancellationToken cancellationToken = default)
    {
        if (customerUserId == Guid.Empty)
        {
            return ServiceResult<BusinessCustomerLookupResponse>.Failure("validation_error", "El usuario es obligatorio.");
        }

        ServiceResult authorization = await EnsureCanSearchCustomersAsync(requesterUserId, isAdmin, cancellationToken);
        if (!authorization.Succeeded)
        {
            return ServiceResult<BusinessCustomerLookupResponse>.Failure(
                authorization.ErrorCode ?? "forbidden",
                authorization.ErrorMessage ?? "Sin permisos.");
        }

        UserAccount? user = await _userRepository.GetByIdAsync(customerUserId, cancellationToken);
        if (user is null || !IsSearchableCustomer(user))
        {
            return ServiceResult<BusinessCustomerLookupResponse>.Failure("not_found", "No se ha encontrado ningun usuario cliente activo con ese identificador.");
        }

        return ServiceResult<BusinessCustomerLookupResponse>.Success(await BuildCustomerLookupResponseAsync(user, "UserId", cancellationToken));
    }

    public async Task<ServiceResult<UserSummaryResponse>> UpdateProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        Domain.Entities.UserAccount? user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return ServiceResult<UserSummaryResponse>.Failure("not_found", "Usuario no encontrado.");
        }

        if (request.BirthDate.HasValue && request.BirthDate.Value.Date > DateTime.UtcNow.Date)
        {
            return ServiceResult<UserSummaryResponse>.Failure("validation_error", "La fecha de nacimiento no puede ser futura.");
        }

        user.FirstName = NormalizeNullable(request.FirstName);
        user.LastName = NormalizeNullable(request.LastName);
        user.DisplayName = NormalizeNullable(request.DisplayName)
            ?? BuildDisplayName(user.FirstName, user.LastName, user.UserName);
        user.Gender = request.Gender;
        user.BirthDate = request.BirthDate;
        user.Language = NormalizeNullable(request.Language);
        user.TimeZone = NormalizeNullable(request.TimeZone);
        user.CountryCode = NormalizeNullable(request.CountryCode);
        user.Region = NormalizeNullable(request.Region);
        user.PostalCode = NormalizePostalCode(request.PostalCode);
        user.AvatarUrl = NormalizeNullable(request.AvatarUrl);
        user.UpdatedAtUtc = DateTime.UtcNow;

        _userRepository.Update(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

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

    private static string? NormalizePostalCode(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static string BuildDisplayName(string? firstName, string? lastName, string userName)
    {
        string displayName = $"{firstName} {lastName}".Trim();
        return string.IsNullOrWhiteSpace(displayName) ? userName.Trim() : displayName;
    }

    private async Task<ServiceResult> EnsureCanSearchCustomersAsync(Guid requesterUserId, bool isAdmin, CancellationToken cancellationToken)
    {
        if (isAdmin)
        {
            return ServiceResult.Success();
        }

        IReadOnlyCollection<NegocioUsuarioVinculacion> links =
            await _negocioUsuarioVinculacionRepository.GetActiveByUserIdAsync(requesterUserId, cancellationToken);

        DateTime now = DateTime.UtcNow;
        bool hasEligibleBusiness = links.Any(link =>
            link.Activa &&
            !link.RevokedAtUtc.HasValue &&
            (!link.FechaInicioUtc.HasValue || link.FechaInicioUtc.Value <= now) &&
            (!link.FechaFinUtc.HasValue || link.FechaFinUtc.Value >= now) &&
            link.Negocio is not null &&
            link.Negocio.Activo &&
            !link.Negocio.IsDeleted &&
            (link.PuedeGestionarNegocio ||
             link.PuedeGestionarPuntos ||
             link.PuedeValidarTickets ||
             link.TipoVinculacion is TipoVinculacionNegocioUsuario.Propietario or TipoVinculacionNegocioUsuario.Gerente));

        return hasEligibleBusiness
            ? ServiceResult.Success()
            : ServiceResult.Failure("forbidden", "El usuario no esta vinculado a ningun negocio activo con permisos para sumar puntos.");
    }

    private async Task<BusinessCustomerLookupResponse> BuildCustomerLookupResponseAsync(
        UserAccount user,
        string matchType,
        CancellationToken cancellationToken)
    {
        string userCode = await _userCodeDirectoryService.EnsureUserCodeAsync(user.Id, cancellationToken);

        return new BusinessCustomerLookupResponse
        {
            UserId = user.Id,
            UserCode = userCode,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            FirstName = user.FirstName,
            LastName = user.LastName,
            DisplayName = user.DisplayName,
            Status = user.Status,
            RegistrationCompleted = user.RegistrationCompleted,
            MatchType = matchType,
            CreatedAtUtc = user.CreatedAtUtc
        };
    }

    private static bool IsSearchableCustomer(UserAccount user)
        => user.Role == UserRole.User &&
           user.Status == UserStatus.Active &&
           user.RegistrationCompleted;

    private static ContactInfo? ParseContact(string? contact)
    {
        string? trimmedContact = NormalizeNullable(contact);
        if (trimmedContact is null)
        {
            return null;
        }

        if (IsEmail(trimmedContact))
        {
            return new ContactInfo(ContactType.Email, trimmedContact.ToUpperInvariant());
        }

        string normalizedPhone = NormalizePhone(trimmedContact);
        return normalizedPhone.Length >= 7
            ? new ContactInfo(ContactType.Phone, normalizedPhone)
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

    private static string NormalizePhone(string value)
        => new(value.Where(char.IsDigit).ToArray());

    private enum ContactType
    {
        Email,
        Phone
    }

    private sealed record ContactInfo(ContactType Type, string NormalizedValue);
}
