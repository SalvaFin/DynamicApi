using System.Net.Mail;
using Dynamic.Fidelity.Application.Contracts.Services;
using Dynamic.Negocios.Application.Contracts.Repositories;
using Dynamic.Negocios.Application.DTOs.Responses;
using Dynamic.Negocios.Application.Mappings;
using Dynamic.Negocios.Domain.Entities;
using Dynamic.Negocios.Domain.Enums;
using Dynamic.Negocios.Infrastructure.Persistence;
using Dynamic.Users.Application.Common;
using Dynamic.Users.Application.Contracts.Repositories;
using Dynamic.Users.Application.Contracts.Services;
using Dynamic.Users.Application.DTOs.Requests;
using Dynamic.Users.Application.DTOs.Responses;
using Dynamic.Users.Application.Mappings;
using Dynamic.Users.Domain.Entities;
using Dynamic.Users.Domain.Enums;
using Dynamic.Users.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dynamic.Users.Application.Services;

public class BusinessUserProvisioningService : IBusinessUserProvisioningService
{
    private readonly DynamicUsersDbContext _usersDbContext;
    private readonly DynamicNegociosDbContext _negociosDbContext;
    private readonly IUserRepository _userRepository;
    private readonly IUserAuthEventRepository _userAuthEventRepository;
    private readonly IPasswordHasher<UserAccount> _passwordHasher;
    private readonly IUserCodeDirectoryService _userCodeDirectoryService;
    private readonly INegocioRepository _negocioRepository;
    private readonly INegocioUsuarioVinculacionRepository _negocioUsuarioVinculacionRepository;
    private readonly ILogger<BusinessUserProvisioningService> _logger;

    public BusinessUserProvisioningService(
        DynamicUsersDbContext usersDbContext,
        DynamicNegociosDbContext negociosDbContext,
        IUserRepository userRepository,
        IUserAuthEventRepository userAuthEventRepository,
        IPasswordHasher<UserAccount> passwordHasher,
        IUserCodeDirectoryService userCodeDirectoryService,
        INegocioRepository negocioRepository,
        INegocioUsuarioVinculacionRepository negocioUsuarioVinculacionRepository,
        ILogger<BusinessUserProvisioningService> logger)
    {
        _usersDbContext = usersDbContext;
        _negociosDbContext = negociosDbContext;
        _userRepository = userRepository;
        _userAuthEventRepository = userAuthEventRepository;
        _passwordHasher = passwordHasher;
        _userCodeDirectoryService = userCodeDirectoryService;
        _negocioRepository = negocioRepository;
        _negocioUsuarioVinculacionRepository = negocioUsuarioVinculacionRepository;
        _logger = logger;
    }

    public async Task<ServiceResult<IReadOnlyCollection<BusinessUserAccountResponse>>> GetBusinessAccountsByAdminAsync(
        Guid negocioId,
        CancellationToken cancellationToken = default)
    {
        Negocio? negocio = await _negocioRepository.GetByIdAsync(negocioId, cancellationToken);
        if (negocio is null || negocio.IsDeleted)
        {
            return ServiceResult<IReadOnlyCollection<BusinessUserAccountResponse>>.Failure("not_found", "El negocio no existe.");
        }

        List<NegocioUsuarioVinculacion> vinculaciones = await _negociosDbContext.NegociosUsuariosVinculaciones
            .Where(vinculacion => vinculacion.NegocioId == negocioId)
            .OrderByDescending(vinculacion => vinculacion.TipoVinculacion == TipoVinculacionNegocioUsuario.Propietario)
            .ThenByDescending(vinculacion => vinculacion.EsPrincipal)
            .ThenBy(vinculacion => vinculacion.TituloRelacion)
            .ToListAsync(cancellationToken);

        List<Guid> userIds = vinculaciones
            .Select(vinculacion => vinculacion.UserId)
            .Distinct()
            .ToList();

        if (userIds.Count == 0)
        {
            return ServiceResult<IReadOnlyCollection<BusinessUserAccountResponse>>.Success([]);
        }

        Dictionary<Guid, UserAccount> users = await _usersDbContext.Users
            .Where(user =>
                userIds.Contains(user.Id) &&
                (user.Role == UserRole.PropietarioNegocio ||
                 user.Role == UserRole.TrabajadorNegocio))
            .ToDictionaryAsync(user => user.Id, cancellationToken);

        List<BusinessUserAccountResponse> response = [];

        foreach (NegocioUsuarioVinculacion vinculacion in vinculaciones)
        {
            if (!users.TryGetValue(vinculacion.UserId, out UserAccount? user))
            {
                continue;
            }

            string? userCode = await _userCodeDirectoryService.GetUserCodeAsync(user.Id, cancellationToken);

            response.Add(new BusinessUserAccountResponse
            {
                NegocioId = negocioId,
                IsOwner = user.Role == UserRole.PropietarioNegocio ||
                    vinculacion.TipoVinculacion == TipoVinculacionNegocioUsuario.Propietario,
                User = user.ToResponse(userCode),
                Vinculacion = vinculacion.ToResponse()
            });
        }

        return ServiceResult<IReadOnlyCollection<BusinessUserAccountResponse>>.Success(response);
    }

    public async Task<ServiceResult<ProvisionedBusinessUserResponse>> CreateOwnerAccountByAdminAsync(
        Guid negocioId,
        CreateBusinessManagedUserRequest request,
        CancellationToken cancellationToken = default)
        => await CreateBusinessUserAsync(
            negocioId,
            request,
            role: UserRole.PropietarioNegocio,
            requesterUserId: null,
            isAdmin: true,
            ownerRoute: true,
            cancellationToken);

    public async Task<ServiceResult<ProvisionedBusinessUserResponse>> CreateWorkerAccountByAdminAsync(
        Guid negocioId,
        CreateBusinessManagedUserRequest request,
        CancellationToken cancellationToken = default)
        => await CreateBusinessUserAsync(
            negocioId,
            request,
            role: UserRole.TrabajadorNegocio,
            requesterUserId: null,
            isAdmin: true,
            ownerRoute: false,
            cancellationToken);

    public async Task<ServiceResult<ProvisionedBusinessUserResponse>> CreateWorkerAccountByOwnerAsync(
        Guid negocioId,
        Guid requesterUserId,
        bool isAdmin,
        CreateBusinessManagedUserRequest request,
        CancellationToken cancellationToken = default)
        => await CreateBusinessUserAsync(
            negocioId,
            request,
            role: UserRole.TrabajadorNegocio,
            requesterUserId,
            isAdmin,
            ownerRoute: false,
            cancellationToken);

    private async Task<ServiceResult<ProvisionedBusinessUserResponse>> CreateBusinessUserAsync(
        Guid negocioId,
        CreateBusinessManagedUserRequest request,
        UserRole role,
        Guid? requesterUserId,
        bool isAdmin,
        bool ownerRoute,
        CancellationToken cancellationToken)
    {
        Negocio? negocio = await _negocioRepository.GetByIdAsync(negocioId, cancellationToken);
        if (negocio is null || negocio.IsDeleted)
        {
            return ServiceResult<ProvisionedBusinessUserResponse>.Failure("not_found", "El negocio no existe.");
        }

        if (!isAdmin)
        {
            if (!requesterUserId.HasValue)
            {
                return ServiceResult<ProvisionedBusinessUserResponse>.Failure("forbidden", "No se ha podido identificar al usuario autenticado.");
            }

            ServiceResult authorization = await EnsureCanProvisionWorkersAsync(negocio, requesterUserId.Value, cancellationToken);
            if (!authorization.Succeeded)
            {
                return ServiceResult<ProvisionedBusinessUserResponse>.Failure(
                    authorization.ErrorCode ?? "forbidden",
                    authorization.ErrorMessage ?? "Sin permisos.");
            }
        }

        if (ownerRoute && negocio.OwnerUserId.HasValue)
        {
            return ServiceResult<ProvisionedBusinessUserResponse>.Failure("conflict", "El negocio ya tiene un propietario asignado.");
        }

        ServiceResult validation = await ValidateRequestAsync(request, cancellationToken);
        if (!validation.Succeeded)
        {
            return ServiceResult<ProvisionedBusinessUserResponse>.Failure(
                validation.ErrorCode ?? "validation_error",
                validation.ErrorMessage ?? "Los datos del usuario no son válidos.");
        }

        DateTime now = DateTime.UtcNow;
        UserAccount user = BuildUser(request, role, now);

        await _userRepository.AddAsync(user, cancellationToken);
        await _usersDbContext.SaveChangesAsync(cancellationToken);

        try
        {
            NegocioUsuarioVinculacion vinculacion = await UpsertNegocioLinkAsync(
                negocio,
                user.Id,
                ownerRoute,
                request,
                now,
                cancellationToken);

            await PersistProvisioningEventAsync(user, cancellationToken);
            string? userCode = await EnsureUserCodeSafeAsync(user.Id, cancellationToken);

            return ServiceResult<ProvisionedBusinessUserResponse>.Success(new ProvisionedBusinessUserResponse
            {
                NegocioId = negocio.Id,
                OwnerAssigned = ownerRoute,
                User = user.ToResponse(userCode),
                Vinculacion = vinculacion.ToResponse()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando usuario de negocio {Role} para negocio {NegocioId}", role, negocioId);
            await TryRollbackUserAsync(user, cancellationToken);
            return ServiceResult<ProvisionedBusinessUserResponse>.Failure("server_error", "No se pudo crear la cuenta del negocio.");
        }
    }

    private async Task<ServiceResult> EnsureCanProvisionWorkersAsync(Negocio negocio, Guid requesterUserId, CancellationToken cancellationToken)
    {
        if (negocio.OwnerUserId == requesterUserId)
        {
            return ServiceResult.Success();
        }

        NegocioUsuarioVinculacion? link =
            await _negocioUsuarioVinculacionRepository.GetByNegocioAndUserAsync(negocio.Id, requesterUserId, cancellationToken);

        if (link is null || !link.Activa || link.RevokedAtUtc.HasValue)
        {
            return ServiceResult.Failure("forbidden", "El usuario no está vinculado al negocio como propietario.");
        }

        DateTime now = DateTime.UtcNow;
        bool outsideDateWindow =
            (link.FechaInicioUtc.HasValue && link.FechaInicioUtc.Value > now) ||
            (link.FechaFinUtc.HasValue && link.FechaFinUtc.Value < now);

        if (outsideDateWindow)
        {
            return ServiceResult.Failure("forbidden", "La vinculación con el negocio no está activa.");
        }

        return link.TipoVinculacion == TipoVinculacionNegocioUsuario.Propietario
            ? ServiceResult.Success()
            : ServiceResult.Failure("forbidden", "Solo el propietario del negocio puede crear trabajadores.");
    }

    private async Task<ServiceResult> ValidateRequestAsync(CreateBusinessManagedUserRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.ConfirmPassword))
        {
            return ServiceResult.Failure("validation_error", "UserName, contraseña y confirmación son obligatorios.");
        }

        if (request.Password != request.ConfirmPassword)
        {
            return ServiceResult.Failure("validation_error", "La confirmación de contraseña no coincide.");
        }

        if (string.IsNullOrWhiteSpace(request.Email) && string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return ServiceResult.Failure("validation_error", "Debes indicar al menos un email o un número de teléfono.");
        }

        if (request.FechaFinUtc.HasValue && request.FechaInicioUtc.HasValue && request.FechaFinUtc.Value < request.FechaInicioUtc.Value)
        {
            return ServiceResult.Failure("validation_error", "La fecha fin no puede ser anterior a la fecha inicio.");
        }

        string normalizedUserName = request.UserName.Trim().ToUpperInvariant();
        if (await _userRepository.GetByUserNameAsync(normalizedUserName, cancellationToken) is not null)
        {
            return ServiceResult.Failure("conflict", "Ya existe un usuario con ese nombre.");
        }

        ContactInfo? emailContact = null;
        ContactInfo? phoneContact = null;

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            emailContact = ParseContact(request.Email);
            if (emailContact is null || emailContact.Type != ContactType.Email)
            {
                return ServiceResult.Failure("validation_error", "El email indicado no es válido.");
            }

            if (await _userRepository.GetByEmailAsync(emailContact.NormalizedValue, cancellationToken) is not null)
            {
                return ServiceResult.Failure("conflict", "Ya existe un usuario con ese email.");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            phoneContact = ParseContact(request.PhoneNumber);
            if (phoneContact is null || phoneContact.Type != ContactType.Phone)
            {
                return ServiceResult.Failure("validation_error", "El teléfono indicado no es válido.");
            }

            if (await _userRepository.GetByPhoneAsync(phoneContact.NormalizedValue, cancellationToken) is not null)
            {
                return ServiceResult.Failure("conflict", "Ya existe un usuario con ese número de teléfono.");
            }
        }

        return ServiceResult.Success();
    }

    private UserAccount BuildUser(CreateBusinessManagedUserRequest request, UserRole role, DateTime now)
    {
        ContactInfo? emailContact = !string.IsNullOrWhiteSpace(request.Email) ? ParseContact(request.Email) : null;
        ContactInfo? phoneContact = !string.IsNullOrWhiteSpace(request.PhoneNumber) ? ParseContact(request.PhoneNumber) : null;

        UserAccount user = new()
        {
            Id = Guid.NewGuid(),
            Email = emailContact?.OriginalValue,
            NormalizedEmail = emailContact?.NormalizedValue,
            UserName = request.UserName.Trim(),
            NormalizedUserName = request.UserName.Trim().ToUpperInvariant(),
            FirstName = Normalize(request.FirstName),
            LastName = Normalize(request.LastName),
            DisplayName = BuildDisplayName(request.FirstName, request.LastName, request.UserName),
            PhoneNumber = phoneContact?.OriginalValue,
            NormalizedPhoneNumber = phoneContact?.NormalizedValue,
            EmailConfirmed = emailContact is not null,
            PhoneNumberConfirmed = phoneContact is not null,
            RegistrationCompleted = true,
            RegistrationInitiatedAtUtc = now,
            RegistrationCompletedAtUtc = now,
            Role = role,
            Status = UserStatus.Active,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            LastSeenAtUtc = now
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        return user;
    }

    private async Task<NegocioUsuarioVinculacion> UpsertNegocioLinkAsync(
        Negocio negocio,
        Guid userId,
        bool ownerRoute,
        CreateBusinessManagedUserRequest request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        NegocioUsuarioVinculacion? existing =
            await _negocioUsuarioVinculacionRepository.GetByNegocioAndUserAsync(negocio.Id, userId, cancellationToken);

        NegocioUsuarioVinculacion vinculacion = existing ?? new NegocioUsuarioVinculacion
        {
            Id = Guid.NewGuid(),
            NegocioId = negocio.Id,
            UserId = userId,
            CreatedAtUtc = now,
            FechaInvitacionUtc = now
        };

        if (ownerRoute)
        {
            negocio.OwnerUserId = userId;
            negocio.UpdatedAtUtc = now;
            _negocioRepository.Update(negocio);

            vinculacion.TipoVinculacion = TipoVinculacionNegocioUsuario.Propietario;
            vinculacion.TituloRelacion = "Propietario";
            vinculacion.EsPrincipal = true;
            vinculacion.PuedeAccederBackoffice = true;
            vinculacion.PuedeGestionarNegocio = true;
            vinculacion.PuedeGestionarClientes = true;
            vinculacion.PuedeGestionarCampanas = true;
            vinculacion.PuedeGestionarPuntos = true;
            vinculacion.PuedeValidarTickets = true;
            vinculacion.PuedeVerReportes = true;
            vinculacion.OrigenVinculacion = "admin_owner_register";
        }
        else
        {
            vinculacion.TipoVinculacion = TipoVinculacionNegocioUsuario.Trabajador;
            vinculacion.TituloRelacion = Normalize(request.TituloRelacion) ?? "Trabajador";
            vinculacion.EsPrincipal = request.EsPrincipal;
            vinculacion.PuedeAccederBackoffice = request.PuedeAccederBackoffice;
            vinculacion.PuedeGestionarNegocio = request.PuedeGestionarNegocio;
            vinculacion.PuedeGestionarClientes = request.PuedeGestionarClientes;
            vinculacion.PuedeGestionarCampanas = request.PuedeGestionarCampanas;
            vinculacion.PuedeGestionarPuntos = request.PuedeGestionarPuntos;
            vinculacion.PuedeValidarTickets = request.PuedeValidarTickets;
            vinculacion.PuedeVerReportes = request.PuedeVerReportes;
            vinculacion.NotasInternas = Normalize(request.NotasInternas);
            vinculacion.OrigenVinculacion = Normalize(request.OrigenVinculacion) ?? "business_worker_register";
        }

        vinculacion.Activa = true;
        vinculacion.FechaAceptacionUtc ??= now;
        vinculacion.FechaInicioUtc = request.FechaInicioUtc ?? vinculacion.FechaInicioUtc ?? now;
        vinculacion.FechaFinUtc = request.FechaFinUtc;
        vinculacion.RevokedAtUtc = null;
        vinculacion.UnlinkedByUserId = null;
        vinculacion.UpdatedAtUtc = now;

        if (existing is null)
        {
            await _negocioUsuarioVinculacionRepository.AddAsync(vinculacion, cancellationToken);
        }
        else
        {
            _negocioUsuarioVinculacionRepository.Update(vinculacion);
        }

        await _negociosDbContext.SaveChangesAsync(cancellationToken);
        return vinculacion;
    }

    private async Task PersistProvisioningEventAsync(UserAccount user, CancellationToken cancellationToken)
    {
        try
        {
            await _userAuthEventRepository.AddAsync(
                new UserAuthEvent
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    EventType = AuthEventType.ClassicRegisterCreated,
                    Identity = user.Email ?? user.PhoneNumber ?? user.UserName,
                    Succeeded = true,
                    CreatedAtUtc = DateTime.UtcNow
                },
                cancellationToken);

            await _usersDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo registrar el evento de creación para el usuario {UserId}", user.Id);
        }
    }

    private async Task<string?> EnsureUserCodeSafeAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            return await _userCodeDirectoryService.EnsureUserCodeAsync(userId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo generar el código de usuario para {UserId}", userId);
            return null;
        }
    }

    private async Task TryRollbackUserAsync(UserAccount user, CancellationToken cancellationToken)
    {
        try
        {
            _userRepository.Remove(user);
            await _usersDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo revertir la creación del usuario {UserId} tras un error de provisión", user.Id);
        }
    }

    private static string BuildDisplayName(string? firstName, string? lastName, string userName)
    {
        string displayName = $"{firstName} {lastName}".Trim();
        return string.IsNullOrWhiteSpace(displayName) ? userName.Trim() : displayName;
    }

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

    private static string NormalizePhone(string value)
        => new(value.Where(char.IsDigit).ToArray());

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private enum ContactType
    {
        Email,
        Phone
    }

    private sealed record ContactInfo(ContactType Type, string OriginalValue, string NormalizedValue);
}
