using Dynamic.Negocios.Application.Common;
using Dynamic.Negocios.Application.Contracts.Repositories;
using Dynamic.Negocios.Application.Contracts.Services;
using Dynamic.Negocios.Application.DTOs.Requests;
using Dynamic.Negocios.Application.DTOs.Responses;
using Dynamic.Negocios.Application.Mappings;
using Dynamic.Negocios.Domain.Entities;
using Dynamic.Negocios.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Dynamic.Negocios.Application.Services;

public class NegocioService : INegocioService
{
    private readonly DynamicNegociosDbContext _dbContext;
    private readonly INegocioRepository _negocioRepository;
    private readonly INegocioUsuarioVinculacionRepository _negocioUsuarioVinculacionRepository;
    private readonly ILogger<NegocioService> _logger;

    public NegocioService(
        DynamicNegociosDbContext dbContext,
        INegocioRepository negocioRepository,
        INegocioUsuarioVinculacionRepository negocioUsuarioVinculacionRepository,
        ILogger<NegocioService> logger)
    {
        _dbContext = dbContext;
        _negocioRepository = negocioRepository;
        _negocioUsuarioVinculacionRepository = negocioUsuarioVinculacionRepository;
        _logger = logger;
    }

    public async Task<ServiceResult<IReadOnlyCollection<NegocioResponse>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<NegocioResponse> negocios = (await _negocioRepository.GetAllAsync(cancellationToken))
            .Select(negocio => negocio.ToResponse())
            .ToArray();

        return ServiceResult<IReadOnlyCollection<NegocioResponse>>.Success(negocios);
    }

    public async Task<ServiceResult<NegocioResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Negocio? negocio = await _negocioRepository.GetByIdAsync(id, cancellationToken);
        return negocio is null
            ? ServiceResult<NegocioResponse>.Failure("not_found", "Negocio no encontrado.")
            : ServiceResult<NegocioResponse>.Success(negocio.ToResponse());
    }

    public async Task<ServiceResult<NegocioResponse>> CreateAsync(CrearNegocioRequest request, CancellationToken cancellationToken = default)
    {
        string? validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return ServiceResult<NegocioResponse>.Failure("validation_error", validationError);
        }

        string normalizedSlug = request.SlugPortal.Trim().ToLowerInvariant();
        if (await _negocioRepository.GetBySlugAsync(normalizedSlug, cancellationToken) is not null)
        {
            return ServiceResult<NegocioResponse>.Failure("conflict", "Ya existe un negocio con ese slug.");
        }

        Negocio negocio = request.ToEntity();
        request.Apply(negocio);
        negocio.Id = Guid.NewGuid();
        negocio.CreatedAtUtc = DateTime.UtcNow;
        negocio.UpdatedAtUtc = negocio.CreatedAtUtc;
        negocio.IsDeleted = false;

        await _negocioRepository.AddAsync(negocio, cancellationToken);
        await EnsureOwnerLinkAsync(negocio, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<NegocioResponse>.Success(negocio.ToResponse());
    }

    public async Task<ServiceResult<NegocioResponse>> UpdateAsync(Guid id, ActualizarNegocioRequest request, CancellationToken cancellationToken = default)
    {
        string? validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return ServiceResult<NegocioResponse>.Failure("validation_error", validationError);
        }

        Negocio? negocio = await _negocioRepository.GetByIdAsync(id, cancellationToken);
        if (negocio is null)
        {
            return ServiceResult<NegocioResponse>.Failure("not_found", "Negocio no encontrado.");
        }

        string normalizedSlug = request.SlugPortal.Trim().ToLowerInvariant();
        Negocio? existingBySlug = await _negocioRepository.GetBySlugAsync(normalizedSlug, cancellationToken);
        if (existingBySlug is not null && existingBySlug.Id != id)
        {
            return ServiceResult<NegocioResponse>.Failure("conflict", "Ya existe otro negocio con ese slug.");
        }

        request.Apply(negocio);
        negocio.UpdatedAtUtc = DateTime.UtcNow;
        _negocioRepository.Update(negocio);

        await EnsureOwnerLinkAsync(negocio, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<NegocioResponse>.Success(negocio.ToResponse());
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Negocio? negocio = await _negocioRepository.GetByIdAsync(id, cancellationToken);
        if (negocio is null)
        {
            return ServiceResult.Failure("not_found", "Negocio no encontrado.");
        }

        negocio.IsDeleted = true;
        negocio.Activo = false;
        negocio.Estado = Domain.Enums.EstadoNegocio.Archivado;
        negocio.DeletedAtUtc = DateTime.UtcNow;
        negocio.UpdatedAtUtc = negocio.DeletedAtUtc.Value;
        _negocioRepository.Update(negocio);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success();
    }

    private static string? ValidateRequest(CrearNegocioRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NombreComercial))
        {
            return "El nombre comercial es obligatorio.";
        }

        if (string.IsNullOrWhiteSpace(request.SlugPortal))
        {
            return "El slug del portal es obligatorio.";
        }

        return request.SlugPortal.Any(char.IsWhiteSpace)
            ? "El slug del portal no puede contener espacios."
            : null;
    }

    private async Task EnsureOwnerLinkAsync(Negocio negocio, CancellationToken cancellationToken)
    {
        if (!negocio.OwnerUserId.HasValue)
        {
            return;
        }

        DateTime now = DateTime.UtcNow;
        NegocioUsuarioVinculacion? existing = await _negocioUsuarioVinculacionRepository.GetByNegocioAndUserAsync(
            negocio.Id,
            negocio.OwnerUserId.Value,
            cancellationToken);

        if (existing is null)
        {
            await _negocioUsuarioVinculacionRepository.AddAsync(
                new NegocioUsuarioVinculacion
                {
                    Id = Guid.NewGuid(),
                    NegocioId = negocio.Id,
                    UserId = negocio.OwnerUserId.Value,
                    TipoVinculacion = Domain.Enums.TipoVinculacionNegocioUsuario.Propietario,
                    TituloRelacion = "Propietario",
                    Activa = true,
                    EsPrincipal = true,
                    PuedeAccederBackoffice = true,
                    PuedeGestionarNegocio = true,
                    PuedeGestionarClientes = true,
                    PuedeGestionarCampanas = true,
                    PuedeGestionarPuntos = true,
                    PuedeValidarTickets = true,
                    PuedeVerReportes = true,
                    OrigenVinculacion = "owner_user_id",
                    FechaInvitacionUtc = now,
                    FechaAceptacionUtc = now,
                    FechaInicioUtc = now,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                },
                cancellationToken);

            return;
        }

        existing.TipoVinculacion = Domain.Enums.TipoVinculacionNegocioUsuario.Propietario;
        existing.TituloRelacion = "Propietario";
        existing.Activa = true;
        existing.EsPrincipal = true;
        existing.PuedeAccederBackoffice = true;
        existing.PuedeGestionarNegocio = true;
        existing.PuedeGestionarClientes = true;
        existing.PuedeGestionarCampanas = true;
        existing.PuedeGestionarPuntos = true;
        existing.PuedeValidarTickets = true;
        existing.PuedeVerReportes = true;
        existing.OrigenVinculacion = existing.OrigenVinculacion ?? "owner_user_id";
        existing.FechaAceptacionUtc ??= now;
        existing.FechaInicioUtc ??= now;
        existing.RevokedAtUtc = null;
        existing.UnlinkedByUserId = null;
        existing.UpdatedAtUtc = now;
        _negocioUsuarioVinculacionRepository.Update(existing);
    }
}
