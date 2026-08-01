using Dynamic.Fidelity.Application.Common;
using Dynamic.Fidelity.Application.Contracts.Repositories;
using Dynamic.Fidelity.Application.Contracts.Services;
using Dynamic.Fidelity.Application.DTOs.Requests;
using Dynamic.Fidelity.Application.DTOs.Responses;
using Dynamic.Fidelity.Application.Mappings;
using Dynamic.Fidelity.Application.Models;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Domain.Enums;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Dynamic.Negocios.Application.Contracts.Repositories;
using Dynamic.Negocios.Domain.Entities;
using Dynamic.Negocios.Domain.Enums;
using Dynamic.Negocios.Infrastructure.Persistence;

namespace Dynamic.Fidelity.Application.Services;

public class TicketService : ITicketService
{
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromDays(365);

    private readonly DynamicFidelityDbContext _dbContext;
    private readonly DynamicNegociosDbContext _negociosDbContext;
    private readonly ITicketRepository _ticketRepository;
    private readonly IPointsRepository _pointsRepository;
    private readonly INegocioRepository _negocioRepository;
    private readonly INegocioUsuarioVinculacionRepository _negocioUsuarioVinculacionRepository;
    private readonly IPointsService _pointsService;
    private readonly ITicketEventPublisher _ticketEventPublisher;

    public TicketService(
        DynamicFidelityDbContext dbContext,
        DynamicNegociosDbContext negociosDbContext,
        ITicketRepository ticketRepository,
        IPointsRepository pointsRepository,
        INegocioRepository negocioRepository,
        INegocioUsuarioVinculacionRepository negocioUsuarioVinculacionRepository,
        IPointsService pointsService,
        ITicketEventPublisher ticketEventPublisher)
    {
        _dbContext = dbContext;
        _negociosDbContext = negociosDbContext;
        _ticketRepository = ticketRepository;
        _pointsRepository = pointsRepository;
        _negocioRepository = negocioRepository;
        _negocioUsuarioVinculacionRepository = negocioUsuarioVinculacionRepository;
        _pointsService = pointsService;
        _ticketEventPublisher = ticketEventPublisher;
    }

    public async Task<ServiceResult<IReadOnlyCollection<TicketResponse>>> GetPublicGeneralTicketsAsync(
        Guid negocioId,
        bool includeWelcomeTicket = false,
        CancellationToken cancellationToken = default)
    {
        Negocio? negocio = await _negocioRepository.GetByIdAsync(negocioId, cancellationToken);
        if (!IsBusinessPubliclyAvailable(negocio))
        {
            return ServiceResult<IReadOnlyCollection<TicketResponse>>.Failure("not_found", "Negocio no encontrado.");
        }

        IReadOnlyCollection<TicketResponse> tickets = (await _ticketRepository.GetTemplatesByNegocioAsync(negocioId, cancellationToken))
            .Where(ticket => IsPublicGeneralTemplateAvailable(ticket) ||
                includeWelcomeTicket && IsPublicWelcomeTemplateAvailable(ticket, negocio!.BonoBienvenidaTicketId))
            .OrderBy(ticket => ticket.PuntosCoste)
            .ThenBy(ticket => ticket.Nombre)
            .Select(ticket => ticket.ToResponse())
            .ToArray();

        return ServiceResult<IReadOnlyCollection<TicketResponse>>.Success(tickets);
    }

    public async Task<ServiceResult<IReadOnlyCollection<TicketResponse>>> GetPublicGeneralTicketsBySlugAsync(
        string slugPortal,
        bool includeWelcomeTicket = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slugPortal))
        {
            return ServiceResult<IReadOnlyCollection<TicketResponse>>.Failure("validation_error", "El slug del negocio es obligatorio.");
        }

        Negocio? negocio = await _negocioRepository.GetByPublicIdentifierAsync(slugPortal.Trim(), cancellationToken);
        if (!IsBusinessPubliclyAvailable(negocio))
        {
            return ServiceResult<IReadOnlyCollection<TicketResponse>>.Failure("not_found", "Negocio no encontrado.");
        }

        Guid resolvedNegocioId = negocio!.Id;
        IReadOnlyCollection<TicketResponse> tickets = (await _ticketRepository.GetTemplatesByNegocioAsync(resolvedNegocioId, cancellationToken))
            .Where(ticket => IsPublicGeneralTemplateAvailable(ticket) ||
                includeWelcomeTicket && IsPublicWelcomeTemplateAvailable(ticket, negocio!.BonoBienvenidaTicketId))
            .OrderBy(ticket => ticket.PuntosCoste)
            .ThenBy(ticket => ticket.Nombre)
            .Select(ticket => ticket.ToResponse())
            .ToArray();

        return ServiceResult<IReadOnlyCollection<TicketResponse>>.Success(tickets);
    }

    public async Task<ServiceResult<IReadOnlyCollection<TicketResponse>>> GetAllAsync(
        Guid negocioId,
        Guid requesterUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        ServiceResult authorization = await EnsureCanManageTicketsAsync(negocioId, requesterUserId, isAdmin, cancellationToken);
        if (!authorization.Succeeded)
        {
            return ServiceResult<IReadOnlyCollection<TicketResponse>>.Failure(
                authorization.ErrorCode ?? "forbidden",
                authorization.ErrorMessage ?? "Sin permisos.");
        }

        IReadOnlyCollection<TicketResponse> tickets = (await _ticketRepository.GetTemplatesByNegocioAsync(negocioId, cancellationToken))
            .Select(ticket => ticket.ToResponse())
            .ToArray();

        return ServiceResult<IReadOnlyCollection<TicketResponse>>.Success(tickets);
    }

    public async Task<ServiceResult<TicketResponse>> GetByIdAsync(
        Guid negocioId,
        Guid ticketId,
        Guid requesterUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        ServiceResult authorization = await EnsureCanManageTicketsAsync(negocioId, requesterUserId, isAdmin, cancellationToken);
        if (!authorization.Succeeded)
        {
            return ServiceResult<TicketResponse>.Failure(
                authorization.ErrorCode ?? "forbidden",
                authorization.ErrorMessage ?? "Sin permisos.");
        }

        Ticket? ticket = await GetTemplateTicketAsync(negocioId, ticketId, cancellationToken);
        if (ticket is null)
        {
            return ServiceResult<TicketResponse>.Failure("not_found", "El ticket no existe o no pertenece al negocio.");
        }

        return ServiceResult<TicketResponse>.Success(ticket.ToResponse());
    }

    public async Task<ServiceResult<TicketResponse>> CreateAsync(
        Guid negocioId,
        Guid requesterUserId,
        bool isAdmin,
        CreateTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        ServiceResult authorization = await EnsureCanManageTicketsAsync(negocioId, requesterUserId, isAdmin, cancellationToken);
        if (!authorization.Succeeded)
        {
            return ServiceResult<TicketResponse>.Failure(
                authorization.ErrorCode ?? "forbidden",
                authorization.ErrorMessage ?? "Sin permisos.");
        }

        ServiceResult validation = ValidateRequest(
            request.Nombre,
            request.Tipo,
            request.CategoriaEnvioEspecial,
            request.Valor,
            request.PuntosCoste,
            request.MaxUsosPorCliente,
            request.ValidezDiasDesdeAsignacion,
            request.AvailableFromUtc,
            request.ExpiresAtUtc);
        if (!validation.Succeeded)
        {
            return ServiceResult<TicketResponse>.Failure(
                validation.ErrorCode ?? "validation_error",
                validation.ErrorMessage ?? "Los datos del ticket no son válidos.");
        }

        if (request.Activo &&
            request.CategoriaEnvioEspecial == CategoriaEnvioTicket.PrimerRegistro &&
            await _ticketRepository.ExistsActiveTemplateByCategoryAsync(
                negocioId,
                CategoriaEnvioTicket.PrimerRegistro,
                cancellationToken: cancellationToken))
        {
            return ServiceResult<TicketResponse>.Failure(
                "conflict",
                "El negocio ya tiene un ticket de bienvenida activo.");
        }

        DateTime now = DateTime.UtcNow;
        Ticket ticket = new()
        {
            Id = Guid.NewGuid(),
            NegocioId = negocioId,
            UserId = null,
            Nombre = request.Nombre.Trim(),
            Descripcion = Normalize(request.Descripcion),
            Tipo = request.Tipo,
            CategoriaEnvioEspecial = request.CategoriaEnvioEspecial,
            Valor = NormalizeValue(request.Valor),
            PuntosCoste = ResolvePointsCoste(request.Tipo, request.PuntosCoste),
            TituloCanje = request.Nombre.Trim(),
            InstruccionesCanje = Normalize(request.Descripcion),
            MaxUsosPorCliente = ResolveMaxUsosPorCliente(request.MaxUsosPorCliente, request.EsDeUnSoloUso),
            UsosConsumidos = 0,
            ValidezDiasDesdeAsignacion = request.ValidezDiasDesdeAsignacion,
            RequiereValidacionManual = request.RequiereValidacionManual,
            EsDeUnSoloUso = ResolveIsSingleUse(request.MaxUsosPorCliente, request.EsDeUnSoloUso),
            EsPlantilla = true,
            Activo = request.Activo,
            Publicado = ResolvePublicado(request.Tipo, request.Publicado),
            AvailableFromUtc = request.AvailableFromUtc,
            ExpiresAtUtc = request.ExpiresAtUtc ?? now.Add(DefaultExpiration),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        ApplyValorByTipo(ticket, request.Tipo, ticket.Valor);

        await _ticketRepository.AddAsync(ticket, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<TicketResponse>.Success(ticket.ToResponse());
    }

    public async Task<ServiceResult<TicketResponse>> CreateWelcomeTicketAsync(
        Guid negocioId,
        Guid requesterUserId,
        bool isAdmin,
        CreateTicketRequest request,
        CancellationToken cancellationToken = default)
        => await CreateConfiguredBusinessTicketAsync(
            negocioId,
            requesterUserId,
            isAdmin,
            CopyWithCategory(request, CategoriaEnvioTicket.PrimerRegistro),
            (negocio, ticketId) => negocio.BonoBienvenidaTicketId = ticketId,
            cancellationToken);

    public async Task<ServiceResult<TicketResponse>> CreateReferralTicketAsync(
        Guid negocioId,
        Guid requesterUserId,
        bool isAdmin,
        CreateTicketRequest request,
        CancellationToken cancellationToken = default)
        => await CreateConfiguredBusinessTicketAsync(
            negocioId,
            requesterUserId,
            isAdmin,
            CopyWithCategory(request, CategoriaEnvioTicket.InvitacionClienteNuevo),
            (negocio, ticketId) => negocio.BonoInvitacionNuevoClienteTicketId = ticketId,
            cancellationToken);

    public async Task<ServiceResult<TicketResponse>> UpdateAsync(
        Guid negocioId,
        Guid ticketId,
        Guid requesterUserId,
        bool isAdmin,
        UpdateTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        ServiceResult authorization = await EnsureCanManageTicketsAsync(negocioId, requesterUserId, isAdmin, cancellationToken);
        if (!authorization.Succeeded)
        {
            return ServiceResult<TicketResponse>.Failure(
                authorization.ErrorCode ?? "forbidden",
                authorization.ErrorMessage ?? "Sin permisos.");
        }

        ServiceResult validation = ValidateRequest(
            request.Nombre,
            request.Tipo,
            request.CategoriaEnvioEspecial,
            request.Valor,
            request.PuntosCoste,
            request.MaxUsosPorCliente,
            request.ValidezDiasDesdeAsignacion,
            request.AvailableFromUtc,
            request.ExpiresAtUtc);
        if (!validation.Succeeded)
        {
            return ServiceResult<TicketResponse>.Failure(
                validation.ErrorCode ?? "validation_error",
                validation.ErrorMessage ?? "Los datos del ticket no son válidos.");
        }

        Ticket? ticket = await GetTemplateTicketAsync(negocioId, ticketId, cancellationToken);
        if (ticket is null)
        {
            return ServiceResult<TicketResponse>.Failure("not_found", "El ticket no existe o no pertenece al negocio.");
        }

        if (request.Activo &&
            request.CategoriaEnvioEspecial == CategoriaEnvioTicket.PrimerRegistro &&
            await _ticketRepository.ExistsActiveTemplateByCategoryAsync(
                negocioId,
                CategoriaEnvioTicket.PrimerRegistro,
                ticket.Id,
                cancellationToken))
        {
            return ServiceResult<TicketResponse>.Failure(
                "conflict",
                "El negocio ya tiene otro ticket de bienvenida activo.");
        }

        ticket.Nombre = request.Nombre.Trim();
        ticket.Descripcion = Normalize(request.Descripcion);
        ticket.Tipo = request.Tipo;
        ticket.CategoriaEnvioEspecial = request.CategoriaEnvioEspecial;
        ticket.Valor = NormalizeValue(request.Valor);
        ticket.PuntosCoste = ResolvePointsCoste(request.Tipo, request.PuntosCoste);
        ticket.TituloCanje = request.Nombre.Trim();
        ticket.InstruccionesCanje = Normalize(request.Descripcion);
        ticket.MaxUsosPorCliente = ResolveMaxUsosPorCliente(request.MaxUsosPorCliente, request.EsDeUnSoloUso);
        ticket.ValidezDiasDesdeAsignacion = request.ValidezDiasDesdeAsignacion;
        ticket.RequiereValidacionManual = request.RequiereValidacionManual;
        ticket.EsDeUnSoloUso = ResolveIsSingleUse(request.MaxUsosPorCliente, request.EsDeUnSoloUso);
        ticket.Activo = request.Activo;
        ticket.Publicado = ResolvePublicado(request.Tipo, request.Publicado);
        ticket.AvailableFromUtc = request.AvailableFromUtc;
        ticket.ExpiresAtUtc = request.ExpiresAtUtc ?? ticket.ExpiresAtUtc;
        ticket.UpdatedAtUtc = DateTime.UtcNow;

        ApplyValorByTipo(ticket, request.Tipo, ticket.Valor);

        _ticketRepository.Update(ticket);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<TicketResponse>.Success(ticket.ToResponse());
    }

    private async Task<ServiceResult<TicketResponse>> CreateConfiguredBusinessTicketAsync(
        Guid negocioId,
        Guid requesterUserId,
        bool isAdmin,
        CreateTicketRequest request,
        Action<Negocio, Guid> configureBusiness,
        CancellationToken cancellationToken)
    {
        ServiceResult<TicketResponse> result = await CreateAsync(
            negocioId,
            requesterUserId,
            isAdmin,
            request,
            cancellationToken);

        if (!result.Succeeded || result.Data is null)
        {
            return result;
        }

        Negocio? negocio = await _negocioRepository.GetByIdAsync(negocioId, cancellationToken);
        if (negocio is null || negocio.IsDeleted)
        {
            return ServiceResult<TicketResponse>.Failure("not_found", "El negocio no existe.");
        }

        configureBusiness(negocio, result.Data.Id);
        negocio.UpdatedAtUtc = DateTime.UtcNow;
        _negocioRepository.Update(negocio);
        await _negociosDbContext.SaveChangesAsync(cancellationToken);

        return result;
    }

    public async Task<ServiceResult<TicketResponse>> UnlockAsync(
        Guid negocioId,
        Guid ticketId,
        Guid requesterUserId,
        CancellationToken cancellationToken = default)
    {
        Ticket? template = await GetTemplateTicketAsync(negocioId, ticketId, cancellationToken);
        if (template is null)
        {
            return ServiceResult<TicketResponse>.Failure("not_found", "El ticket no existe o no pertenece al negocio.");
        }

        if (!template.Activo || !template.Publicado)
        {
            return ServiceResult<TicketResponse>.Failure("conflict", "El ticket no est\u00e1 disponible para desbloqueo.");
        }

        if (template.CategoriaEnvioEspecial != CategoriaEnvioTicket.General)
        {
            return ServiceResult<TicketResponse>.Failure("validation_error", "Solo los tickets generales pueden desbloquearse con puntos.");
        }

        if (!template.PuntosCoste.HasValue || template.PuntosCoste.Value <= 0)
        {
            return ServiceResult<TicketResponse>.Failure("validation_error", "El ticket no tiene un precio en puntos v\u00e1lido.");
        }

        ServiceResult authorization = await EnsureUserHasPointsAccountAsync(negocioId, requesterUserId, cancellationToken);
        if (!authorization.Succeeded)
        {
            return ServiceResult<TicketResponse>.Failure(
                authorization.ErrorCode ?? "forbidden",
                authorization.ErrorMessage ?? "El usuario no tiene cuenta de puntos en este negocio.");
        }

        DateTime now = DateTime.UtcNow;
        if (template.AvailableFromUtc.HasValue && template.AvailableFromUtc.Value > now)
        {
            return ServiceResult<TicketResponse>.Failure("conflict", "El ticket todav\u00eda no est\u00e1 disponible.");
        }

        if (template.ExpiresAtUtc <= now)
        {
            return ServiceResult<TicketResponse>.Failure("conflict", "El ticket ya ha expirado.");
        }

        int currentAssignments =
            await _ticketRepository.CountAssignedToUserByTemplateAsync(requesterUserId, template.Id, cancellationToken);

        if (template.MaxUsosPorCliente.HasValue && currentAssignments >= template.MaxUsosPorCliente.Value)
        {
            return ServiceResult<TicketResponse>.Failure("conflict", "El usuario ya ha alcanzado el m\u00e1ximo de desbloqueos permitidos para este ticket.");
        }

        ServiceResult<PointsSummary> spendResult = await _pointsService.SpendPointsAsync(
            requesterUserId,
            negocioId,
            template.PuntosCoste.Value,
            reason: $"Desbloqueo del ticket {template.Nombre}",
            reference: template.Id.ToString("N"),
            cancellationToken: cancellationToken);

        if (!spendResult.Succeeded)
        {
            return ServiceResult<TicketResponse>.Failure(
                spendResult.ErrorCode ?? "validation_error",
                spendResult.ErrorMessage ?? "No se ha podido descontar el saldo de puntos.");
        }

        Ticket assignedTicket = BuildAssignedTicket(template, requesterUserId, now);

        await _ticketRepository.AddAsync(assignedTicket, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _ticketEventPublisher.PublishReceivedAsync(assignedTicket, "points_unlock", cancellationToken);

        return ServiceResult<TicketResponse>.Success(assignedTicket.ToResponse());
    }

    public async Task<ServiceResult> DeleteAsync(
        Guid negocioId,
        Guid ticketId,
        Guid requesterUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        ServiceResult authorization = await EnsureCanManageTicketsAsync(negocioId, requesterUserId, isAdmin, cancellationToken);
        if (!authorization.Succeeded)
        {
            return ServiceResult.Failure(
                authorization.ErrorCode ?? "forbidden",
                authorization.ErrorMessage ?? "Sin permisos.");
        }

        Ticket? ticket = await GetTemplateTicketAsync(negocioId, ticketId, cancellationToken);
        if (ticket is null)
        {
            return ServiceResult.Failure("not_found", "El ticket no existe o no pertenece al negocio.");
        }

        _ticketRepository.Remove(ticket);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success();
    }

    private async Task<ServiceResult> EnsureCanManageTicketsAsync(
        Guid negocioId,
        Guid requesterUserId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        Negocio? negocio = await _negocioRepository.GetByIdAsync(negocioId, cancellationToken);
        if (negocio is null || negocio.IsDeleted)
        {
            return ServiceResult.Failure("not_found", "El negocio no existe.");
        }

        if (isAdmin)
        {
            return ServiceResult.Success();
        }

        NegocioUsuarioVinculacion? link =
            await _negocioUsuarioVinculacionRepository.GetByNegocioAndUserAsync(negocioId, requesterUserId, cancellationToken);

        if (link is null || !link.Activa)
        {
            return ServiceResult.Failure("forbidden", "El usuario no está vinculado al negocio.");
        }

        DateTime now = DateTime.UtcNow;
        bool outsideDateWindow =
            (link.FechaInicioUtc.HasValue && link.FechaInicioUtc.Value > now) ||
            (link.FechaFinUtc.HasValue && link.FechaFinUtc.Value < now) ||
            link.RevokedAtUtc.HasValue;

        if (outsideDateWindow)
        {
            return ServiceResult.Failure("forbidden", "La vinculación del usuario con el negocio no está activa actualmente.");
        }

        bool isManager = link.TipoVinculacion is TipoVinculacionNegocioUsuario.Propietario or TipoVinculacionNegocioUsuario.Gerente;
        if (!isManager && !link.PuedeGestionarNegocio)
        {
            return ServiceResult.Failure("forbidden", "El usuario no tiene permisos para gestionar tickets de este negocio.");
        }

        return ServiceResult.Success();
    }

    private async Task<Ticket?> GetTemplateTicketAsync(Guid negocioId, Guid ticketId, CancellationToken cancellationToken)
    {
        Ticket? ticket = await _ticketRepository.GetByIdAsync(ticketId, cancellationToken);
        if (ticket is null || ticket.NegocioId != negocioId || !ticket.EsPlantilla || ticket.UserId.HasValue)
        {
            return null;
        }

        return ticket;
    }

    private static ServiceResult ValidateRequest(
        string? nombre,
        TipoTicket tipo,
        CategoriaEnvioTicket categoriaEnvioEspecial,
        decimal? valor,
        int? puntosCoste,
        int? maxUsosPorCliente,
        int? validezDiasDesdeAsignacion,
        DateTime? availableFromUtc,
        DateTime? expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return ServiceResult.Failure("validation_error", "El nombre del ticket es obligatorio.");
        }

        if (!Enum.IsDefined(tipo))
        {
            return ServiceResult.Failure("validation_error", "El tipo de ticket debe ser Porcentual, ValorFijo, Libre o Promocion.");
        }

        if (availableFromUtc.HasValue && expiresAtUtc.HasValue && expiresAtUtc.Value < availableFromUtc.Value)
        {
            return ServiceResult.Failure("validation_error", "La fecha de expiración no puede ser anterior a la fecha de disponibilidad.");
        }

        if (valor.HasValue && valor.Value < 0)
        {
            return ServiceResult.Failure("validation_error", "El valor del ticket no puede ser negativo.");
        }

        if (puntosCoste.HasValue && puntosCoste.Value < 0)
        {
            return ServiceResult.Failure("validation_error", "El precio en puntos del ticket no puede ser negativo.");
        }

        if (categoriaEnvioEspecial != CategoriaEnvioTicket.General &&
            puntosCoste.HasValue &&
            puntosCoste.Value > 0)
        {
            return ServiceResult.Failure("validation_error", "Los tickets especiales de registro o invitaci\u00f3n no deben configurarse con precio en puntos.");
        }

        if (maxUsosPorCliente.HasValue && maxUsosPorCliente.Value <= 0)
        {
            return ServiceResult.Failure("validation_error", "El uso máximo por cliente debe ser mayor que 0.");
        }

        if (validezDiasDesdeAsignacion.HasValue && validezDiasDesdeAsignacion.Value <= 0)
        {
            return ServiceResult.Failure("validation_error", "La validez en días desde la asignación debe ser mayor que 0.");
        }

        if (tipo == TipoTicket.Promocion &&
            puntosCoste.HasValue &&
            puntosCoste.Value > 0)
        {
            return ServiceResult.Failure("validation_error", "Un ticket de promoción no debe configurarse con precio en puntos.");
        }

        if (tipo == TipoTicket.Porcentual)
        {
            if (!valor.HasValue || valor.Value <= 0 || valor.Value > 100)
            {
                return ServiceResult.Failure("validation_error", "Un ticket porcentual debe tener un valor entre 0 y 100.");
            }
        }
        else if (tipo == TipoTicket.ValorFijo && (!valor.HasValue || valor.Value <= 0))
        {
            return ServiceResult.Failure("validation_error", "Un ticket de importe fijo debe tener un valor mayor que 0.");
        }
        else if (tipo == TipoTicket.Libre && valor.HasValue && valor.Value != 0)
        {
            return ServiceResult.Failure("validation_error", "Un ticket libre debe tener un valor igual a 0 o nulo.");
        }

        return ServiceResult.Success();
    }

    private static void ApplyValorByTipo(Ticket ticket, TipoTicket tipo, decimal? valor)
    {
        ticket.DescuentoPorcentaje = tipo == TipoTicket.Porcentual ? valor : null;
        ticket.DescuentoImporteFijo = tipo == TipoTicket.ValorFijo ? valor : null;

        if (tipo is TipoTicket.Libre or TipoTicket.Promocion)
        {
            ticket.BeneficioEspecialResumen = ticket.Nombre;
            ticket.BeneficioEspecialDetalle = ticket.Descripcion;
        }
        else
        {
            ticket.BeneficioEspecialResumen = null;
            ticket.BeneficioEspecialDetalle = null;
        }
    }

    private static decimal? NormalizeValue(decimal? value)
        => value.HasValue
            ? decimal.Round(value.Value, 2, MidpointRounding.AwayFromZero)
            : null;

    private static int? NormalizePointsCoste(int? puntosCoste)
        => puntosCoste.HasValue && puntosCoste.Value > 0 ? puntosCoste.Value : null;

    private static int? ResolvePointsCoste(TipoTicket tipo, int? puntosCoste)
        => tipo == TipoTicket.Promocion ? null : NormalizePointsCoste(puntosCoste);

    private static bool ResolvePublicado(TipoTicket tipo, bool publicado)
        => tipo != TipoTicket.Promocion && publicado;

    private static int? ResolveMaxUsosPorCliente(int? maxUsosPorCliente, bool esDeUnSoloUso)
        => maxUsosPorCliente ?? (esDeUnSoloUso ? 1 : null);

    private static bool ResolveIsSingleUse(int? maxUsosPorCliente, bool esDeUnSoloUso)
        => maxUsosPorCliente.HasValue ? maxUsosPorCliente.Value <= 1 : esDeUnSoloUso;

    private static CreateTicketRequest CopyWithCategory(CreateTicketRequest request, CategoriaEnvioTicket category)
        => new()
        {
            Nombre = request.Nombre,
            Descripcion = request.Descripcion,
            Tipo = request.Tipo,
            CategoriaEnvioEspecial = category,
            Valor = request.Valor,
            PuntosCoste = request.PuntosCoste,
            MaxUsosPorCliente = request.MaxUsosPorCliente,
            ValidezDiasDesdeAsignacion = request.ValidezDiasDesdeAsignacion,
            Activo = request.Activo,
            Publicado = request.Publicado,
            EsDeUnSoloUso = request.EsDeUnSoloUso,
            RequiereValidacionManual = request.RequiereValidacionManual,
            AvailableFromUtc = request.AvailableFromUtc,
            ExpiresAtUtc = request.ExpiresAtUtc
        };

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsBusinessPubliclyAvailable(Negocio? negocio)
        => negocio is not null &&
           !negocio.IsDeleted &&
           negocio.Activo &&
           negocio.PublicadoPortal;

    private static bool IsPublicGeneralTemplateAvailable(Ticket ticket)
    {
        DateTime now = DateTime.UtcNow;

        if (!ticket.EsPlantilla ||
            ticket.UserId.HasValue ||
            !ticket.Activo ||
            !ticket.Publicado ||
            ticket.Tipo == TipoTicket.Promocion ||
            ticket.CategoriaEnvioEspecial != CategoriaEnvioTicket.General ||
            !ticket.PuntosCoste.HasValue ||
            ticket.PuntosCoste.Value <= 0)
        {
            return false;
        }

        if (ticket.AvailableFromUtc.HasValue && ticket.AvailableFromUtc.Value > now)
        {
            return false;
        }

        return ticket.ExpiresAtUtc > now;
    }

    private static bool IsPublicWelcomeTemplateAvailable(Ticket ticket, Guid? configuredWelcomeTicketId)
    {
        DateTime now = DateTime.UtcNow;

        if (!configuredWelcomeTicketId.HasValue ||
            ticket.Id != configuredWelcomeTicketId.Value ||
            !ticket.EsPlantilla ||
            ticket.UserId.HasValue ||
            !ticket.Activo ||
            !ticket.Publicado ||
            ticket.CategoriaEnvioEspecial != CategoriaEnvioTicket.PrimerRegistro ||
            ticket.PuntosCoste.GetValueOrDefault() > 0)
        {
            return false;
        }

        if (ticket.AvailableFromUtc.HasValue && ticket.AvailableFromUtc.Value > now)
        {
            return false;
        }

        return ticket.ExpiresAtUtc > now;
    }

    private async Task<ServiceResult> EnsureUserHasPointsAccountAsync(Guid negocioId, Guid userId, CancellationToken cancellationToken)
    {
        Points? points = await _pointsRepository.GetByUserAndNegocioAsync(userId, negocioId, cancellationToken);
        if (points is null)
        {
            return ServiceResult.Failure("forbidden", "El usuario no tiene cuenta de puntos en este negocio.");
        }

        return ServiceResult.Success();
    }

    private static Ticket BuildAssignedTicket(Ticket template, Guid userId, DateTime now)
        => new()
        {
            Id = Guid.NewGuid(),
            NegocioId = template.NegocioId,
            UserId = userId,
            ParentTicketId = template.Id,
            Nombre = template.Nombre,
            Descripcion = template.Descripcion,
            Tipo = template.Tipo,
            CategoriaEnvioEspecial = template.CategoriaEnvioEspecial,
            Valor = template.Valor,
            CodigoInterno = template.CodigoInterno,
            CodigoVisible = $"{template.CodigoVisible ?? "UNLOCK"}-{Guid.NewGuid():N}"[..20],
            TituloCanje = template.TituloCanje,
            InstruccionesCanje = template.InstruccionesCanje,
            CondicionesUso = template.CondicionesUso,
            MensajeMarketing = template.MensajeMarketing,
            DescuentoPorcentaje = template.DescuentoPorcentaje,
            DescuentoImporteFijo = template.DescuentoImporteFijo,
            BeneficioEspecialResumen = template.BeneficioEspecialResumen,
            BeneficioEspecialDetalle = template.BeneficioEspecialDetalle,
            GastoMinimoRequerido = template.GastoMinimoRequerido,
            PuntosCoste = template.PuntosCoste,
            MaxUsosPorCliente = template.MaxUsosPorCliente,
            UsosConsumidos = 0,
            ValidezDiasDesdeAsignacion = template.ValidezDiasDesdeAsignacion,
            RequiereValidacionManual = template.RequiereValidacionManual,
            EsDeUnSoloUso = template.EsDeUnSoloUso,
            EsPlantilla = false,
            Activo = template.Activo,
            Publicado = template.Publicado,
            Usado = false,
            CreatedAtUtc = now,
            AvailableFromUtc = template.AvailableFromUtc ?? now,
            ExpiresAtUtc = ResolveAssignedExpiration(template, now),
            UpdatedAtUtc = now
        };

    private static DateTime ResolveAssignedExpiration(Ticket template, DateTime assignedAtUtc)
        => template.ValidezDiasDesdeAsignacion.HasValue
            ? assignedAtUtc.AddDays(template.ValidezDiasDesdeAsignacion.Value)
            : template.ExpiresAtUtc;
}
