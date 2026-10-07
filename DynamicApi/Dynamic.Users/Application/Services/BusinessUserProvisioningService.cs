using System.Net.Mail;
using System.Text.Json;
using Dynamic.Notify.Application.Contracts;
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
    private readonly IEmailNotificationService _emailNotificationService;
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
        IEmailNotificationService emailNotificationService,
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
        _emailNotificationService = emailNotificationService;
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
            .Where(vinculacion => vinculacion.NegocioId == negocioId && vinculacion.RevokedAtUtc == null)
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
                IsOwner = vinculacion.TipoVinculacion == TipoVinculacionNegocioUsuario.Propietario,
                User = user.ToResponse(userCode),
                Vinculacion = vinculacion.ToResponse()
            });
        }

        return ServiceResult<IReadOnlyCollection<BusinessUserAccountResponse>>.Success(response);
    }

    public async Task<ServiceResult<IReadOnlyCollection<BusinessUserAccountResponse>>> GetBusinessAccountsByOwnerAsync(
        Guid negocioId, Guid requesterUserId, CancellationToken cancellationToken = default)
    {
        ServiceResult access = await EnsureOwnerAccessAsync(negocioId, requesterUserId, cancellationToken);
        return access.Succeeded
            ? await GetBusinessAccountsByAdminAsync(negocioId, cancellationToken)
            : ServiceResult<IReadOnlyCollection<BusinessUserAccountResponse>>.Failure(access.ErrorCode ?? "forbidden", access.ErrorMessage ?? "Sin permisos.");
    }

    public async Task<ServiceResult<ProvisionedBusinessUserResponse>> CreateOwnerAccountByOwnerAsync(
        Guid negocioId, Guid requesterUserId, CreateBusinessManagedUserRequest request,
        CancellationToken cancellationToken = default)
        => await CreateBusinessUserAsync(negocioId, request, UserRole.PropietarioNegocio,
            requesterUserId, false, true, cancellationToken);

    public async Task<ServiceResult<ProvisionedBusinessUserResponse>> UpdateBusinessAccountByOwnerAsync(
        Guid negocioId, Guid userId, Guid requesterUserId, UpdateBusinessManagedUserRequest request,
        string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
    {
        ServiceResult access = await EnsureOwnerAccessAsync(negocioId, requesterUserId, cancellationToken);
        if (!access.Succeeded)
            return ServiceResult<ProvisionedBusinessUserResponse>.Failure(access.ErrorCode ?? "forbidden", access.ErrorMessage ?? "Sin permisos.");

        Negocio? negocio = await _negocioRepository.GetByIdAsync(negocioId, cancellationToken);
        NegocioUsuarioVinculacion? target = await _negocioUsuarioVinculacionRepository
            .GetByNegocioAndUserAsync(negocioId, userId, cancellationToken);
        if (target is null)
            return ServiceResult<ProvisionedBusinessUserResponse>.Failure("not_found", "La cuenta no pertenece a este negocio.");
        TipoVinculacionNegocioUsuario targetRole = request.TipoVinculacion ?? target.TipoVinculacion;
        if (request.EsPrincipal != target.EsPrincipal ||
            (request.EsPrincipal && negocio?.OwnerUserId != userId) ||
            (requesterUserId == userId && targetRole != TipoVinculacionNegocioUsuario.Propietario) ||
            (negocio?.OwnerUserId == userId &&
             (request.Status == UserStatus.Disabled || !request.PuedeAccederBackoffice ||
              (request.FechaInicioUtc.HasValue && request.FechaInicioUtc.Value > DateTime.UtcNow) ||
              (request.FechaFinUtc.HasValue && request.FechaFinUtc.Value <= DateTime.UtcNow))) ||
            (requesterUserId == userId && request.Status == UserStatus.Disabled))
            return ServiceResult<ProvisionedBusinessUserResponse>.Failure("forbidden", "No se puede cambiar el propietario principal ni desactivar esta cuenta.");

        return await UpdateBusinessAccountByAdminAsync(negocioId, userId, requesterUserId,
            request, ipAddress, userAgent, cancellationToken);
    }

    public async Task<ServiceResult> UnlinkBusinessAccountByOwnerAsync(
        Guid negocioId, Guid userId, Guid requesterUserId,
        string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
    {
        ServiceResult access = await EnsureOwnerAccessAsync(negocioId, requesterUserId, cancellationToken);
        if (!access.Succeeded) return access;
        if (requesterUserId == userId)
            return ServiceResult.Failure("forbidden", "No puedes retirar tu propia vinculación de propietario.");

        return await UnlinkBusinessAccountByAdminAsync(
            negocioId, userId, requesterUserId, ipAddress, userAgent, cancellationToken);
    }

    public async Task<ServiceResult<IReadOnlyCollection<BusinessUserAccountAuditResponse>>> GetBusinessAccountAuditByOwnerAsync(
        Guid negocioId, Guid userId, Guid requesterUserId, CancellationToken cancellationToken = default)
    {
        ServiceResult access = await EnsureOwnerAccessAsync(negocioId, requesterUserId, cancellationToken);
        return access.Succeeded
            ? await GetBusinessAccountAuditByAdminAsync(negocioId, userId, cancellationToken)
            : ServiceResult<IReadOnlyCollection<BusinessUserAccountAuditResponse>>.Failure(access.ErrorCode ?? "forbidden", access.ErrorMessage ?? "Sin permisos.");
    }

    private async Task<ServiceResult> EnsureOwnerAccessAsync(
        Guid negocioId, Guid requesterUserId, CancellationToken cancellationToken)
    {
        Negocio? negocio = await _negocioRepository.GetByIdAsync(negocioId, cancellationToken);
        return negocio is null || negocio.IsDeleted
            ? ServiceResult.Failure("not_found", "El negocio no existe.")
            : await EnsureCanProvisionWorkersAsync(negocio, requesterUserId, cancellationToken);
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

    public async Task<ServiceResult<ProvisionedBusinessUserResponse>> UpdateBusinessAccountByAdminAsync(
        Guid negocioId,
        Guid userId,
        Guid modifiedByUserId,
        UpdateBusinessManagedUserRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        Negocio? negocio = await _negocioRepository.GetByIdAsync(negocioId, cancellationToken);
        if (negocio is null || negocio.IsDeleted)
        {
            return ServiceResult<ProvisionedBusinessUserResponse>.Failure("not_found", "El negocio no existe.");
        }

        UserAccount? user = await _usersDbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        NegocioUsuarioVinculacion? link = await _negocioUsuarioVinculacionRepository
            .GetByNegocioAndUserAsync(negocioId, userId, cancellationToken);

        if (user is null || link is null || !link.Activa || link.RevokedAtUtc.HasValue ||
            user.Role is not (UserRole.PropietarioNegocio or UserRole.TrabajadorNegocio) ||
            link.TipoVinculacion is not (TipoVinculacionNegocioUsuario.Propietario or TipoVinculacionNegocioUsuario.Trabajador))
        {
            return ServiceResult<ProvisionedBusinessUserResponse>.Failure("not_found", "La cuenta no pertenece a este negocio.");
        }

        ServiceResult validation = await ValidateUpdateRequestAsync(request, userId, cancellationToken);
        if (!validation.Succeeded)
        {
            return ServiceResult<ProvisionedBusinessUserResponse>.Failure(
                validation.ErrorCode ?? "validation_error",
                validation.ErrorMessage ?? "Los datos de la cuenta no son válidos.");
        }

        TipoVinculacionNegocioUsuario targetRole = request.TipoVinculacion ?? link.TipoVinculacion;
        if (negocio.OwnerUserId == userId && targetRole != TipoVinculacionNegocioUsuario.Propietario)
            return ServiceResult<ProvisionedBusinessUserResponse>.Failure("forbidden", "No se puede cambiar el rol del propietario principal.");
        if (negocio.OwnerUserId == userId && !request.EsPrincipal)
            return ServiceResult<ProvisionedBusinessUserResponse>.Failure("forbidden", "El propietario principal debe seguir marcado como principal.");
        if (request.EsPrincipal && targetRole != TipoVinculacionNegocioUsuario.Propietario)
            return ServiceResult<ProvisionedBusinessUserResponse>.Failure("validation_error", "Solo un propietario puede ser principal.");
        if (targetRole == TipoVinculacionNegocioUsuario.Propietario && !request.PuedeAccederBackoffice)
            return ServiceResult<ProvisionedBusinessUserResponse>.Failure("validation_error", "Un propietario debe poder acceder al backoffice.");

        DateTime now = DateTime.UtcNow;
        UserRole targetUserRole = await ResolveBusinessRoleAsync(userId, negocioId, targetRole, cancellationToken);
        Guid? previousOwnerId = negocio.OwnerUserId != userId ? negocio.OwnerUserId : null;
        NegocioUsuarioVinculacion? previousOwnerLink = request.EsPrincipal && previousOwnerId.HasValue
            ? await _negocioUsuarioVinculacionRepository.GetByNegocioAndUserAsync(negocioId, previousOwnerId.Value, cancellationToken)
            : null;
        bool previousOwnerWasPrincipal = previousOwnerLink?.EsPrincipal ?? false;
        Dictionary<string, object?> changes = new(StringComparer.Ordinal);
        ContactInfo? email = ParseOptionalEmail(request.Email);
        ContactInfo? phone = ParseOptionalPhone(request.PhoneNumber);

        AddChange(changes, "userName", user.UserName, request.UserName.Trim());
        AddChange(changes, "email", user.Email, email?.OriginalValue);
        AddChange(changes, "phoneNumber", user.PhoneNumber, phone?.OriginalValue);
        AddChange(changes, "firstName", user.FirstName, Normalize(request.FirstName));
        AddChange(changes, "lastName", user.LastName, Normalize(request.LastName));
        AddChange(changes, "status", user.Status.ToString(), request.Status.ToString());
        AddChange(changes, "tipoVinculacion", link.TipoVinculacion.ToString(), targetRole.ToString());
        AddChange(changes, "role", user.Role.ToString(), targetUserRole.ToString());
        AddChange(changes, "tituloRelacion", link.TituloRelacion, Normalize(request.TituloRelacion));
        AddChange(changes, "esPrincipal", link.EsPrincipal, request.EsPrincipal);
        AddChange(changes, "puedeAccederBackoffice", link.PuedeAccederBackoffice, request.PuedeAccederBackoffice);
        AddChange(changes, "puedeGestionarNegocio", link.PuedeGestionarNegocio, request.PuedeGestionarNegocio);
        AddChange(changes, "puedeGestionarClientes", link.PuedeGestionarClientes, request.PuedeGestionarClientes);
        AddChange(changes, "puedeGestionarCampanas", link.PuedeGestionarCampanas, request.PuedeGestionarCampanas);
        AddChange(changes, "puedeGestionarPuntos", link.PuedeGestionarPuntos, request.PuedeGestionarPuntos);
        AddChange(changes, "puedeValidarTickets", link.PuedeValidarTickets, request.PuedeValidarTickets);
        AddChange(changes, "puedeVerReportes", link.PuedeVerReportes, request.PuedeVerReportes);
        AddChange(changes, "notasInternas", link.NotasInternas, Normalize(request.NotasInternas));
        AddChange(changes, "origenVinculacion", link.OrigenVinculacion, Normalize(request.OrigenVinculacion));
        AddChange(changes, "fechaInicioUtc", link.FechaInicioUtc, request.FechaInicioUtc);
        AddChange(changes, "fechaFinUtc", link.FechaFinUtc, request.FechaFinUtc);

        user.UserName = request.UserName.Trim();
        user.NormalizedUserName = user.UserName.ToUpperInvariant();
        bool emailUnchanged = string.Equals(user.NormalizedEmail, email?.NormalizedValue, StringComparison.Ordinal);
        bool phoneUnchanged = string.Equals(user.NormalizedPhoneNumber, phone?.NormalizedValue, StringComparison.Ordinal);
        user.Email = email?.OriginalValue;
        user.NormalizedEmail = email?.NormalizedValue;
        user.PhoneNumber = phone?.OriginalValue;
        user.NormalizedPhoneNumber = phone?.NormalizedValue;
        user.FirstName = Normalize(request.FirstName);
        user.LastName = Normalize(request.LastName);
        user.DisplayName = BuildDisplayName(user.FirstName, user.LastName, user.UserName);
        user.EmailConfirmed = email is not null && emailUnchanged && user.EmailConfirmed;
        user.PhoneNumberConfirmed = phone is not null && phoneUnchanged && user.PhoneNumberConfirmed;
        user.Status = request.Status;
        user.UpdatedAtUtc = now;

        link.TituloRelacion = Normalize(request.TituloRelacion);
        link.TipoVinculacion = targetRole;
        link.EsPrincipal = request.EsPrincipal;
        link.PuedeAccederBackoffice = request.PuedeAccederBackoffice;
        link.PuedeGestionarNegocio = request.PuedeGestionarNegocio;
        link.PuedeGestionarClientes = request.PuedeGestionarClientes;
        link.PuedeGestionarCampanas = request.PuedeGestionarCampanas;
        link.PuedeGestionarPuntos = request.PuedeGestionarPuntos;
        link.PuedeValidarTickets = request.PuedeValidarTickets;
        link.PuedeVerReportes = request.PuedeVerReportes;
        link.NotasInternas = Normalize(request.NotasInternas);
        link.OrigenVinculacion = Normalize(request.OrigenVinculacion);
        link.FechaInicioUtc = request.FechaInicioUtc;
        link.FechaFinUtc = request.FechaFinUtc;
        link.UpdatedAtUtc = now;

        if (targetRole == TipoVinculacionNegocioUsuario.Propietario && !negocio.OwnerUserId.HasValue)
        {
            negocio.OwnerUserId = userId;
            link.EsPrincipal = true;
            _negocioRepository.Update(negocio);
        }
        else if (targetRole == TipoVinculacionNegocioUsuario.Propietario && request.EsPrincipal && previousOwnerId.HasValue)
        {
            negocio.OwnerUserId = userId;
            negocio.UpdatedAtUtc = now;
            _negocioRepository.Update(negocio);
            if (previousOwnerLink is not null)
            {
                previousOwnerLink.EsPrincipal = false;
                previousOwnerLink.UpdatedAtUtc = now;
            }
        }

        if (request.Status == UserStatus.Disabled || user.Role != targetUserRole || changes.ContainsKey("tipoVinculacion"))
        {
            IReadOnlyCollection<UserSession> sessions = await _usersDbContext.UserSessions
                .Where(session => session.UserId == userId && session.RevokedAtUtc == null && session.RefreshTokenExpiresAtUtc > now)
                .ToListAsync(cancellationToken);
            foreach (UserSession session in sessions)
            {
                session.RevokedAtUtc = now;
                session.RevocationReason = request.Status == UserStatus.Disabled
                    ? "BusinessAccountDisabledByAdmin" : "BusinessRoleChanged";
            }
        }

        _usersDbContext.BusinessUserAccountAudits.Add(new BusinessUserAccountAudit
        {
            Id = Guid.NewGuid(),
            NegocioId = negocioId,
            UserId = userId,
            ModifiedByUserId = modifiedByUserId,
            ChangesJson = JsonSerializer.Serialize(changes),
            CreatedAtUtc = now,
            IpAddress = ipAddress,
            UserAgent = userAgent
        });

        if (previousOwnerLink is not null && request.EsPrincipal && previousOwnerId.HasValue)
        {
            _usersDbContext.BusinessUserAccountAudits.Add(new BusinessUserAccountAudit
            {
                Id = Guid.NewGuid(), NegocioId = negocioId, UserId = previousOwnerId.Value,
                ModifiedByUserId = modifiedByUserId,
                ChangesJson = JsonSerializer.Serialize(new { esPrincipal = new { Old = previousOwnerWasPrincipal, New = false } }),
                CreatedAtUtc = now, IpAddress = ipAddress, UserAgent = userAgent
            });
        }

        await _negociosDbContext.SaveChangesAsync(cancellationToken);
        user.Role = targetUserRole;
        await _usersDbContext.SaveChangesAsync(cancellationToken);
        string? userCode = await _userCodeDirectoryService.GetUserCodeAsync(user.Id, cancellationToken);

        if (changes.ContainsKey("tipoVinculacion"))
            await SendBusinessAccountNotificationAsync(user, negocio,
                targetRole == TipoVinculacionNegocioUsuario.Propietario, true, cancellationToken);

        return ServiceResult<ProvisionedBusinessUserResponse>.Success(new ProvisionedBusinessUserResponse
        {
            NegocioId = negocioId,
            OwnerAssigned = link.TipoVinculacion == TipoVinculacionNegocioUsuario.Propietario,
            User = user.ToResponse(userCode),
            Vinculacion = link.ToResponse()
        });
    }

    public async Task<ServiceResult> UnlinkBusinessAccountByAdminAsync(
        Guid negocioId, Guid userId, Guid modifiedByUserId,
        string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
    {
        Negocio? negocio = await _negocioRepository.GetByIdAsync(negocioId, cancellationToken);
        if (negocio is null || negocio.IsDeleted)
            return ServiceResult.Failure("not_found", "El negocio no existe.");

        UserAccount? user = await _usersDbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        NegocioUsuarioVinculacion? link = await _negocioUsuarioVinculacionRepository
            .GetByNegocioAndUserAsync(negocioId, userId, cancellationToken);
        if (user is null || link is null || !link.Activa || link.RevokedAtUtc.HasValue ||
            link.TipoVinculacion is not (TipoVinculacionNegocioUsuario.Propietario or TipoVinculacionNegocioUsuario.Trabajador))
            return ServiceResult.Failure("not_found", "La cuenta no está vinculada a este negocio.");
        if (user.Role == UserRole.Admin)
            return ServiceResult.Failure("forbidden", "No se puede modificar una cuenta administradora desde este panel.");
        if (negocio.OwnerUserId == userId)
            return ServiceResult.Failure("forbidden", "No se puede retirar al propietario principal del negocio.");

        DateTime now = DateTime.UtcNow;
        link.Activa = false;
        link.RevokedAtUtc = now;
        link.UnlinkedByUserId = modifiedByUserId;
        link.UpdatedAtUtc = now;

        await _negociosDbContext.SaveChangesAsync(cancellationToken);
        user.Role = await ResolveBusinessRoleAsync(userId, negocioId, null, cancellationToken);
        user.UpdatedAtUtc = now;
        IReadOnlyCollection<UserSession> activeSessions = await _usersDbContext.UserSessions
            .Where(session => session.UserId == userId && session.RevokedAtUtc == null && session.RefreshTokenExpiresAtUtc > now)
            .ToListAsync(cancellationToken);
        foreach (UserSession session in activeSessions)
        {
            session.RevokedAtUtc = now;
            session.RevocationReason = "BusinessAccountUnlinked";
        }
        _usersDbContext.BusinessUserAccountAudits.Add(new BusinessUserAccountAudit
        {
            Id = Guid.NewGuid(), NegocioId = negocioId, UserId = userId,
            ModifiedByUserId = modifiedByUserId,
            ChangesJson = JsonSerializer.Serialize(new { vinculacion = new { Old = "Activa", New = "Retirada" } }),
            CreatedAtUtc = now, IpAddress = ipAddress, UserAgent = userAgent
        });
        await _usersDbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success();
    }

    private async Task<UserRole> ResolveBusinessRoleAsync(
        Guid userId, Guid negocioId, TipoVinculacionNegocioUsuario? currentLinkRole,
        CancellationToken cancellationToken)
    {
        List<TipoVinculacionNegocioUsuario> otherRoles = await _negociosDbContext.NegociosUsuariosVinculaciones
            .Where(link => link.UserId == userId && link.NegocioId != negocioId && link.Activa && link.RevokedAtUtc == null)
            .Select(link => link.TipoVinculacion)
            .ToListAsync(cancellationToken);
        if (currentLinkRole.HasValue) otherRoles.Add(currentLinkRole.Value);

        return otherRoles.Contains(TipoVinculacionNegocioUsuario.Propietario)
            ? UserRole.PropietarioNegocio
            : otherRoles.Any(role => role is TipoVinculacionNegocioUsuario.Trabajador or
                TipoVinculacionNegocioUsuario.Gerente or TipoVinculacionNegocioUsuario.Colaborador or
                TipoVinculacionNegocioUsuario.Soporte)
                ? UserRole.TrabajadorNegocio
                : UserRole.User;
    }

    public async Task<ServiceResult<IReadOnlyCollection<BusinessUserAccountAuditResponse>>> GetBusinessAccountAuditByAdminAsync(
        Guid negocioId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        Negocio? negocio = await _negocioRepository.GetByIdAsync(negocioId, cancellationToken);
        bool belongs = await _negociosDbContext.NegociosUsuariosVinculaciones
            .AnyAsync(link => link.NegocioId == negocioId && link.UserId == userId &&
                              (link.TipoVinculacion == TipoVinculacionNegocioUsuario.Propietario ||
                               link.TipoVinculacion == TipoVinculacionNegocioUsuario.Trabajador),
                cancellationToken);
        if (negocio is null || negocio.IsDeleted || !belongs)
        {
            return ServiceResult<IReadOnlyCollection<BusinessUserAccountAuditResponse>>.Failure("not_found", "La cuenta no pertenece a este negocio.");
        }

        List<BusinessUserAccountAudit> audits = await _usersDbContext.BusinessUserAccountAudits
            .Where(audit => audit.NegocioId == negocioId && audit.UserId == userId)
            .OrderByDescending(audit => audit.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        HashSet<Guid> modifierIds = audits.Select(audit => audit.ModifiedByUserId).ToHashSet();
        Dictionary<Guid, string> names = await _usersDbContext.Users
            .Where(user => modifierIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.UserName, cancellationToken);

        return ServiceResult<IReadOnlyCollection<BusinessUserAccountAuditResponse>>.Success(audits.Select(audit => new BusinessUserAccountAuditResponse
        {
            Id = audit.Id,
            NegocioId = audit.NegocioId,
            UserId = audit.UserId,
            ModifiedByUserId = audit.ModifiedByUserId,
            ModifiedByUserName = names.GetValueOrDefault(audit.ModifiedByUserId),
            Changes = JsonSerializer.Deserialize<Dictionary<string, object?>>(audit.ChangesJson) ?? new(),
            CreatedAtUtc = audit.CreatedAtUtc,
            IpAddress = audit.IpAddress,
            UserAgent = audit.UserAgent
        }).ToArray());
    }

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
            CustomerLinkResult? linkResult = request.LinkToBusiness ? await UpsertCustomerAudienceAsync(
                negocio,
                user.Id,
                requesterUserId,
                now,
                cancellationToken) : null;

            NegocioAudiencia? audience = linkResult?.Audiencia ?? await _negociosDbContext.NegociosAudiencias
                .FirstOrDefaultAsync(item => item.NegocioId == negocio.Id && item.UserId == user.Id, cancellationToken);

            await PersistBackofficeCustomerEventAsync(user, validation.Data.Contact.OriginalValue, ipAddress, userAgent, cancellationToken);
            string? userCode = await EnsureUserCodeSafeAsync(user.Id, cancellationToken);

            bool receivedWelcomeTicket = false;
            if (linkResult?.LinkedNow == true)
            {
                receivedWelcomeTicket =
                    await _registrationRewardService.AssignBusinessWelcomeTicketAsync(negocio.Id, user.Id, cancellationToken);
            }

            string message = !request.LinkToBusiness
                ? created
                    ? "Cliente dado de alta correctamente."
                    : completedPendingUser
                        ? "El registro pendiente del cliente se ha completado correctamente."
                        : "El cliente ya estaba registrado."
                : created
                ? "Cliente dado de alta y vinculado al negocio correctamente."
                : linkResult!.LinkedNow
                    ? "El cliente ya existÃ­a y se ha vinculado al negocio correctamente."
                    : completedPendingUser
                        ? "El registro pendiente del cliente se ha completado y ya estaba vinculado al negocio."
                        : "El cliente ya existÃ­a y ya estaba vinculado al negocio.";

            return ServiceResult<BusinessCustomerRegistrationResponse>.Success(new BusinessCustomerRegistrationResponse
            {
                NegocioId = negocio.Id,
                Created = created,
                ExistingUser = !created,
                LinkedNow = linkResult?.LinkedNow == true,
                AudienciaId = audience?.Id,
                FormaParteAudiencia = IsActiveAudience(audience),
                PermiteCorreosPromocionales = audience?.PermiteCorreosPromocionales == true,
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

        ContactInfo? email = ParseOptionalEmail(request.Email);
        if (!string.IsNullOrWhiteSpace(request.Email) && email is null)
        {
            return ServiceResult<ProvisionedBusinessUserResponse>.Failure("validation_error", "El email indicado no es válido.");
        }

        UserAccount? existingUser = email is null ? null :
            await _userRepository.GetByEmailAsync(email.NormalizedValue, cancellationToken);
        if (existingUser is not null)
        {
            if (existingUser.Role == UserRole.Admin || existingUser.Status != UserStatus.Active)
            {
                return ServiceResult<ProvisionedBusinessUserResponse>.Failure("conflict", "Esta cuenta no se puede vincular al negocio.");
            }

            NegocioUsuarioVinculacion? existingLink = await _negocioUsuarioVinculacionRepository
                .GetByNegocioAndUserAsync(negocioId, existingUser.Id, cancellationToken);
            if (existingLink is { Activa: true, RevokedAtUtc: null })
            {
                return ServiceResult<ProvisionedBusinessUserResponse>.Failure("conflict", "El usuario ya está vinculado a este negocio.");
            }
        }

        ServiceResult validation = existingUser is null
            ? await ValidateRequestAsync(request, cancellationToken)
            : ValidateExistingAccountRequest(request);
        if (!validation.Succeeded)
        {
            return ServiceResult<ProvisionedBusinessUserResponse>.Failure(
                validation.ErrorCode ?? "validation_error",
                validation.ErrorMessage ?? "Los datos del usuario no son válidos.");
        }

        DateTime now = DateTime.UtcNow;
        UserAccount user = existingUser ?? BuildUser(request, role, now);
        UserRole originalRole = user.Role;
        if (existingUser is null)
        {
            await _userRepository.AddAsync(user, cancellationToken);
            await _usersDbContext.SaveChangesAsync(cancellationToken);
        }

        try
        {
            if (existingUser is not null &&
                (ownerRoute && user.Role != UserRole.PropietarioNegocio ||
                 !ownerRoute && user.Role == UserRole.User))
            {
                user.Role = role;
                user.UpdatedAtUtc = now;
                await _usersDbContext.SaveChangesAsync(cancellationToken);
            }

            NegocioUsuarioVinculacion vinculacion = await UpsertNegocioLinkAsync(
                negocio,
                user.Id,
                ownerRoute,
                request,
                now,
                cancellationToken);

            if (existingUser is null)
            {
                await PersistProvisioningEventAsync(user, cancellationToken);
            }
            string? userCode = await EnsureUserCodeSafeAsync(user.Id, cancellationToken);
            await SendBusinessAccountNotificationAsync(user, negocio, ownerRoute, existingUser is not null, cancellationToken);

            return ServiceResult<ProvisionedBusinessUserResponse>.Success(new ProvisionedBusinessUserResponse
            {
                NegocioId = negocio.Id,
                OwnerAssigned = ownerRoute,
                ExistingAccountLinked = existingUser is not null,
                User = user.ToResponse(userCode),
                Vinculacion = vinculacion.ToResponse()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando usuario de negocio {Role} para negocio {NegocioId}", role, negocioId);
            if (existingUser is null)
            {
                await TryRollbackUserAsync(user, cancellationToken);
            }
            else if (user.Role != originalRole)
            {
                try
                {
                    user.Role = originalRole;
                    await _usersDbContext.SaveChangesAsync(cancellationToken);
                }
                catch (Exception rollbackError)
                {
                    _logger.LogError(rollbackError, "No se pudo restaurar el rol del usuario {UserId}", user.Id);
                }
            }
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

        if (request.Password.Length < 8)
        {
            return ServiceResult.Failure("validation_error", "La contraseña debe tener al menos 8 caracteres.");
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

    private static ServiceResult ValidateExistingAccountRequest(CreateBusinessManagedUserRequest request)
    {
        if (request.FechaFinUtc.HasValue && request.FechaInicioUtc.HasValue &&
            request.FechaFinUtc.Value < request.FechaInicioUtc.Value)
        {
            return ServiceResult.Failure("validation_error", "La fecha fin no puede ser anterior a la fecha inicio.");
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

    private async Task SendBusinessAccountNotificationAsync(
        UserAccount user, Negocio negocio, bool ownerRoute, bool existingAccount, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }

        if (!Uri.TryCreate(_userRegistrationOptions.CompletionUrlBase, UriKind.Absolute, out Uri? appUri) ||
            appUri.Scheme is not ("https" or "http"))
        {
            _logger.LogWarning("No se pudo construir el enlace al panel para el negocio {NegocioId}", negocio.Id);
            return;
        }

        string panelPath = ownerRoute ? "/backoffice-owners" : "/worker";
        string panelUrl = new Uri(appUri, panelPath).ToString();

        try
        {
            await _emailNotificationService.SendAsync(
                BusinessAccountEmailTemplate.Build(user, negocio.NombreComercial, ownerRoute, existingAccount, panelUrl),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo enviar el aviso de acceso al usuario {UserId} del negocio {NegocioId}", user.Id, negocio.Id);
        }
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

    private async Task<ServiceResult> ValidateUpdateRequestAsync(
        UpdateBusinessManagedUserRequest request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (request.TipoVinculacion.HasValue &&
            request.TipoVinculacion.Value is not (TipoVinculacionNegocioUsuario.Propietario or TipoVinculacionNegocioUsuario.Trabajador))
        {
            return ServiceResult.Failure("validation_error", "El rol debe ser propietario o trabajador.");
        }

        if (string.IsNullOrWhiteSpace(request.UserName) || request.UserName.Trim().Length > 64)
        {
            return ServiceResult.Failure("validation_error", "El nombre de usuario es obligatorio y debe tener como máximo 64 caracteres.");
        }

        if (request.Status is not (UserStatus.Active or UserStatus.Disabled))
        {
            return ServiceResult.Failure("validation_error", "El estado editable debe ser Active o Disabled.");
        }

        if (request.FechaInicioUtc.HasValue && request.FechaFinUtc.HasValue &&
            request.FechaFinUtc.Value < request.FechaInicioUtc.Value)
        {
            return ServiceResult.Failure("validation_error", "La fecha de fin no puede ser anterior a la fecha de inicio.");
        }

        ContactInfo? email = ParseOptionalEmail(request.Email);
        if (!string.IsNullOrWhiteSpace(request.Email) && email is null)
        {
            return ServiceResult.Failure("validation_error", "El email indicado no es válido.");
        }

        ContactInfo? phone = ParseOptionalPhone(request.PhoneNumber);
        if (!string.IsNullOrWhiteSpace(request.PhoneNumber) && phone is null)
        {
            return ServiceResult.Failure("validation_error", "El teléfono indicado no es válido.");
        }

        string normalizedUserName = request.UserName.Trim().ToUpperInvariant();
        if (await _usersDbContext.Users.AnyAsync(user => user.Id != userId && user.NormalizedUserName == normalizedUserName, cancellationToken))
        {
            return ServiceResult.Failure("conflict", "Ya existe un usuario con ese nombre de usuario.");
        }

        if (email is not null && await _usersDbContext.Users.AnyAsync(user => user.Id != userId && user.NormalizedEmail == email.NormalizedValue, cancellationToken))
        {
            return ServiceResult.Failure("conflict", "Ya existe un usuario con ese email.");
        }

        if (phone is not null && await _usersDbContext.Users.AnyAsync(user => user.Id != userId && user.NormalizedPhoneNumber == phone.NormalizedValue, cancellationToken))
        {
            return ServiceResult.Failure("conflict", "Ya existe un usuario con ese teléfono.");
        }

        return ServiceResult.Success();
    }

    private static ContactInfo? ParseOptionalEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        return IsEmail(trimmed) ? new ContactInfo(ContactType.Email, trimmed, trimmed.ToUpperInvariant()) : null;
    }

    private static ContactInfo? ParseOptionalPhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        string normalized = NormalizePhone(trimmed);
        return normalized.Length >= 7 ? new ContactInfo(ContactType.Phone, trimmed, normalized) : null;
    }

    private static void AddChange(Dictionary<string, object?> changes, string field, object? oldValue, object? newValue)
    {
        if (!Equals(oldValue, newValue))
        {
            changes[field] = new { Old = oldValue, New = newValue };
        }
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
