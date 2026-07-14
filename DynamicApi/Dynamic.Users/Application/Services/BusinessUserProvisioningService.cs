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
using Dynamic.Users.Application.Options;
using Dynamic.Users.Domain.Entities;
using Dynamic.Users.Domain.Enums;
using Dynamic.Users.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    private readonly INegocioAudienciaService _negocioAudienciaService;
    private readonly IRegistrationRewardService _registrationRewardService;
    private readonly UserRegistrationOptions _userRegistrationOptions;
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
        INegocioAudienciaService negocioAudienciaService,
        IRegistrationRewardService registrationRewardService,
        IOptions<UserRegistrationOptions> userRegistrationOptions,
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
        _negocioAudienciaService = negocioAudienciaService;
        _registrationRewardService = registrationRewardService;
        _userRegistrationOptions = userRegistrationOptions.Value;
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

    public async Task<ServiceResult<BusinessCustomerRegistrationResponse>> CreateCustomerByBusinessStaffAsync(
        Guid negocioId,
        Guid requesterUserId,
        bool isAdmin,
        CreateBusinessCustomerUserRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        Negocio? negocio = await _negocioRepository.GetByIdAsync(negocioId, cancellationToken);
        if (negocio is null || negocio.IsDeleted)
        {
            return ServiceResult<BusinessCustomerRegistrationResponse>.Failure("not_found", "El negocio no existe.");
        }

        ServiceResult authorization = await EnsureCanRegisterCustomersAsync(negocio, requesterUserId, isAdmin, cancellationToken);
        if (!authorization.Succeeded)
        {
            return ServiceResult<BusinessCustomerRegistrationResponse>.Failure(
                authorization.ErrorCode ?? "forbidden",
                authorization.ErrorMessage ?? "Sin permisos.");
        }

        ServiceResult<ValidatedCustomerRegistration> validation = await ValidateCustomerRegistrationRequestAsync(request, cancellationToken);
        if (!validation.Succeeded || validation.Data is null)
        {
            return ServiceResult<BusinessCustomerRegistrationResponse>.Failure(
                validation.ErrorCode ?? "validation_error",
                validation.ErrorMessage ?? "Los datos del cliente no son vÃ¡lidos.");
        }

        DateTime now = DateTime.UtcNow;
        bool created = false;
        bool completedPendingUser = false;
        UserAccount user;

        if (validation.Data.ExistingUser is null)
        {
            user = await BuildCustomerUserAsync(request, validation.Data.Contact, now, cancellationToken);
            await _userRepository.AddAsync(user, cancellationToken);
            created = true;
        }
        else
        {
            user = validation.Data.ExistingUser;
            if (user.Role != UserRole.User)
            {
                return ServiceResult<BusinessCustomerRegistrationResponse>.Failure(
                    "conflict",
                    "El contacto pertenece a una cuenta de backoffice y no puede darse de alta como cliente.");
            }

            if (!user.RegistrationCompleted)
            {
                ApplyCustomerRegistrationData(user, request, validation.Data.Contact, now);
                completedPendingUser = true;
                _userRepository.Update(user);
            }
        }

        await _usersDbContext.SaveChangesAsync(cancellationToken);

        try
        {
            CustomerLinkResult linkResult = await UpsertCustomerAudienceAsync(
                negocio,
                user.Id,
                requesterUserId,
                now,
                cancellationToken);

            await PersistBackofficeCustomerEventAsync(user, validation.Data.Contact.OriginalValue, ipAddress, userAgent, cancellationToken);
            string? userCode = await EnsureUserCodeSafeAsync(user.Id, cancellationToken);

            bool receivedWelcomeTicket = false;
            if (linkResult.LinkedNow)
            {
                receivedWelcomeTicket =
                    await _registrationRewardService.AssignBusinessWelcomeTicketAsync(negocio.Id, user.Id, cancellationToken);
            }

            string message = created
                ? "Cliente dado de alta y vinculado al negocio correctamente."
                : linkResult.LinkedNow
                    ? "El cliente ya existÃ­a y se ha vinculado al negocio correctamente."
                    : completedPendingUser
                        ? "El registro pendiente del cliente se ha completado y ya estaba vinculado al negocio."
                        : "El cliente ya existÃ­a y ya estaba vinculado al negocio.";

            return ServiceResult<BusinessCustomerRegistrationResponse>.Success(new BusinessCustomerRegistrationResponse
            {
                NegocioId = negocio.Id,
                Created = created,
                ExistingUser = !created,
                LinkedNow = linkResult.LinkedNow,
                AudienciaId = linkResult.Audiencia.Id,
                FormaParteAudiencia = true,
                ReceivedWelcomeTicket = receivedWelcomeTicket,
                Message = message,
                User = user.ToResponse(userCode)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dando de alta cliente {Contact} para negocio {NegocioId}", request.Contact, negocioId);

            if (created)
            {
                await TryRollbackUserAsync(user, cancellationToken);
            }

            return ServiceResult<BusinessCustomerRegistrationResponse>.Failure("server_error", "No se pudo dar de alta el cliente.");
        }
    }

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

    private async Task<ServiceResult> EnsureCanRegisterCustomersAsync(
        Negocio negocio,
        Guid requesterUserId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        if (isAdmin || negocio.OwnerUserId == requesterUserId)
        {
            return ServiceResult.Success();
        }

        NegocioUsuarioVinculacion? link =
            await _negocioUsuarioVinculacionRepository.GetByNegocioAndUserAsync(negocio.Id, requesterUserId, cancellationToken);

        if (!IsActiveLink(link))
        {
            return ServiceResult.Failure("forbidden", "El usuario no estÃ¡ vinculado al negocio.");
        }

        bool isBusinessStaff = link!.TipoVinculacion is
            TipoVinculacionNegocioUsuario.Propietario or
            TipoVinculacionNegocioUsuario.Gerente or
            TipoVinculacionNegocioUsuario.Trabajador or
            TipoVinculacionNegocioUsuario.Colaborador or
            TipoVinculacionNegocioUsuario.Soporte;

        if (!isBusinessStaff || !link.PuedeAccederBackoffice)
        {
            return ServiceResult.Failure("forbidden", "El usuario vinculado al negocio no puede dar de alta clientes.");
        }

        return ServiceResult.Success();
    }

    private async Task<ServiceResult<ValidatedCustomerRegistration>> ValidateCustomerRegistrationRequestAsync(
        CreateBusinessCustomerUserRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.TermsAccepted || !request.PrivacyPolicyAccepted)
        {
            return ServiceResult<ValidatedCustomerRegistration>.Failure(
                "validation_error",
                "Debes confirmar que el cliente acepta los terminos y la politica de privacidad.");
        }

        if (string.IsNullOrWhiteSpace(request.Contact) ||
            string.IsNullOrWhiteSpace(request.Nombre) ||
            string.IsNullOrWhiteSpace(request.Apellidos) ||
            string.IsNullOrWhiteSpace(request.PostalCode) ||
            !request.Province.HasValue ||
            !Enum.IsDefined(request.Province.Value))
        {
            return ServiceResult<ValidatedCustomerRegistration>.Failure(
                "validation_error",
                "Contacto, nombre, apellidos, codigo postal y provincia son obligatorios.");
        }

        ServiceResult<int> birthDateValidation = ValidateBirthDateForRegistration(request.BirthDate);
        if (!birthDateValidation.Succeeded)
        {
            return ServiceResult<ValidatedCustomerRegistration>.Failure(
                birthDateValidation.ErrorCode ?? "validation_error",
                birthDateValidation.ErrorMessage ?? $"La fecha de nacimiento debe indicar al menos {_userRegistrationOptions.MinimumAge} aÃ±os.");
        }

        ContactInfo? contact = ParseContact(request.Contact);
        if (contact is null)
        {
            return ServiceResult<ValidatedCustomerRegistration>.Failure("validation_error", "El contacto indicado no es vÃ¡lido.");
        }

        UserAccount? existingUser = contact.Type switch
        {
            ContactType.Email => await _userRepository.GetByEmailAsync(contact.NormalizedValue, cancellationToken),
            ContactType.Phone => await _userRepository.GetByPhoneAsync(contact.NormalizedValue, cancellationToken),
            _ => null
        };

        return ServiceResult<ValidatedCustomerRegistration>.Success(new ValidatedCustomerRegistration(contact, existingUser));
    }

    private async Task<UserAccount> BuildCustomerUserAsync(
        CreateBusinessCustomerUserRequest request,
        ContactInfo contact,
        DateTime now,
        CancellationToken cancellationToken)
    {
        string userName = await GenerateUniqueCustomerUserNameAsync(cancellationToken);
        UserAccount user = new()
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Role = UserRole.User,
            Status = UserStatus.Active,
            RegistrationInitiatedAtUtc = now,
            RegistrationCompletedAtUtc = now,
            RegistrationCompleted = true,
            CreatedAtUtc = now
        };

        ApplyCustomerRegistrationData(user, request, contact, now);
        user.PasswordHash = _passwordHasher.HashPassword(user, $"{Guid.NewGuid():N}aA1!");
        user.PasswordIsTemporary = true;

        return user;
    }

    private void ApplyCustomerRegistrationData(
        UserAccount user,
        CreateBusinessCustomerUserRequest request,
        ContactInfo contact,
        DateTime now)
    {
        if (contact.Type == ContactType.Email)
        {
            user.Email = contact.OriginalValue;
            user.NormalizedEmail = contact.NormalizedValue;
            user.EmailConfirmed = true;
        }
        else
        {
            user.PhoneNumber = contact.OriginalValue;
            user.NormalizedPhoneNumber = contact.NormalizedValue;
            user.PhoneNumberConfirmed = true;
        }

        user.FirstName = request.Nombre.Trim();
        user.LastName = request.Apellidos.Trim();
        user.DisplayName = $"{request.Nombre} {request.Apellidos}".Trim();
        user.AgeAtRegistration = null;
        user.BirthDate = request.BirthDate!.Value.Date;
        user.Gender = request.Gender;
        user.PostalCode = Normalize(request.PostalCode)?.ToUpperInvariant();
        user.Province = request.Province!.Value;
        user.RegistrationCompleted = true;
        user.RegistrationCompletedAtUtc = now;
        user.RegistrationValidationToken = null;
        user.RegistrationValidationTokenExpiresAtUtc = null;
        user.Status = UserStatus.Active;
        user.LastSeenAtUtc = now;
        user.UpdatedAtUtc = now;
        user.TermsAccepted = request.TermsAccepted;
        user.TermsAcceptedAtUtc = request.TermsAccepted ? now : null;
        user.PrivacyPolicyAccepted = request.PrivacyPolicyAccepted;
        user.PrivacyPolicyAcceptedAtUtc = request.PrivacyPolicyAccepted ? now : null;
        user.MarketingAccepted = request.MarketingAccepted;
        user.MarketingAcceptedAtUtc = request.MarketingAccepted ? now : null;
    }

    private async Task<CustomerLinkResult> UpsertCustomerAudienceAsync(
        Negocio negocio,
        Guid userId,
        Guid linkedByUserId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        NegocioAudiencia? existing =
            await _negociosDbContext.NegociosAudiencias
                .FirstOrDefaultAsync(audience => audience.NegocioId == negocio.Id && audience.UserId == userId, cancellationToken);

        if (IsActiveAudience(existing))
        {
            return new CustomerLinkResult(existing!, LinkedNow: false);
        }

        Dynamic.Fidelity.Application.Common.ServiceResult<NegocioAudiencia> result =
            await _negocioAudienciaService.EnsureAudienceAsync(
                negocio.Id,
                userId,
                "business_staff_customer_register",
                cancellationToken);

        if (!result.Succeeded || result.Data is null)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "No se pudo crear la audiencia del cliente.");
        }

        return new CustomerLinkResult(result.Data, LinkedNow: true);
    }

    private async Task PersistBackofficeCustomerEventAsync(
        UserAccount user,
        string contact,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        try
        {
            await _userAuthEventRepository.AddAsync(
                new UserAuthEvent
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    EventType = AuthEventType.BackofficeCustomerRegistered,
                    Identity = contact,
                    Succeeded = true,
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    CreatedAtUtc = DateTime.UtcNow
                },
                cancellationToken);

            await _usersDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo registrar el evento de alta backoffice para el usuario {UserId}", user.Id);
        }
    }

    private async Task<string> GenerateUniqueCustomerUserNameAsync(CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            string userName = $"user{Guid.NewGuid():N}"[..16];
            if (await _userRepository.GetByUserNameAsync(userName.ToUpperInvariant(), cancellationToken) is null)
            {
                return userName;
            }
        }

        return $"user{Guid.NewGuid():N}"[..16];
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
        user.PasswordIsTemporary = false;
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
            bool isPrimaryOwner = !negocio.OwnerUserId.HasValue || negocio.OwnerUserId == userId;
            if (!negocio.OwnerUserId.HasValue)
            {
                negocio.OwnerUserId = userId;
                negocio.UpdatedAtUtc = now;
                _negocioRepository.Update(negocio);
            }

            vinculacion.TipoVinculacion = TipoVinculacionNegocioUsuario.Propietario;
            vinculacion.TituloRelacion = "Propietario";
            vinculacion.EsPrincipal = isPrimaryOwner;
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
                $"La edad mÃ­nima para registrarse es de {_userRegistrationOptions.MinimumAge} aÃ±os.");
        }

        if (age > 130)
        {
            return ServiceResult<int>.Failure("validation_error", "La fecha de nacimiento no es vÃ¡lida.");
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

    private static bool IsActiveLink(NegocioUsuarioVinculacion? link)
    {
        if (link is null || !link.Activa || link.RevokedAtUtc.HasValue)
        {
            return false;
        }

        DateTime now = DateTime.UtcNow;
        return (!link.FechaInicioUtc.HasValue || link.FechaInicioUtc.Value <= now) &&
               (!link.FechaFinUtc.HasValue || link.FechaFinUtc.Value >= now);
    }

    private static bool IsActiveAudience(NegocioAudiencia? audience)
        => audience is not null && audience.Activa && !audience.FechaBajaUtc.HasValue;

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

    private sealed record ValidatedCustomerRegistration(ContactInfo Contact, UserAccount? ExistingUser);

    private sealed record CustomerLinkResult(NegocioAudiencia Audiencia, bool LinkedNow);
}
