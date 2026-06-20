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

public class NegocioUsuarioVinculacionService : INegocioUsuarioVinculacionService
{
    private readonly DynamicNegociosDbContext _dbContext;
    private readonly INegocioRepository _negocioRepository;
    private readonly INegocioUsuarioVinculacionRepository _negocioUsuarioVinculacionRepository;
    private readonly ILogger<NegocioUsuarioVinculacionService> _logger;

    public NegocioUsuarioVinculacionService(
        DynamicNegociosDbContext dbContext,
        INegocioRepository negocioRepository,
        INegocioUsuarioVinculacionRepository negocioUsuarioVinculacionRepository,
        ILogger<NegocioUsuarioVinculacionService> logger)
    {
        _dbContext = dbContext;
        _negocioRepository = negocioRepository;
        _negocioUsuarioVinculacionRepository = negocioUsuarioVinculacionRepository;
        _logger = logger;
    }

    public async Task<ServiceResult<IReadOnlyCollection<NegocioVinculadoResponse>>> GetNegociosByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<NegocioVinculadoResponse> negocios = (await _negocioUsuarioVinculacionRepository.GetActiveByUserIdAsync(userId, cancellationToken))
            .Select(vinculacion => vinculacion.ToNegocioVinculadoResponse())
            .ToArray();

        return ServiceResult<IReadOnlyCollection<NegocioVinculadoResponse>>.Success(negocios);
    }

    public async Task<ServiceResult<NegocioVinculadoResponse>> GetPrincipalNegocioByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        NegocioUsuarioVinculacion? vinculacion = (await _negocioUsuarioVinculacionRepository.GetActiveByUserIdAsync(userId, cancellationToken))
            .FirstOrDefault();

        if (vinculacion is null)
        {
            return ServiceResult<NegocioVinculadoResponse>.Failure("not_found", "El usuario no tiene un negocio activo vinculado.");
        }

        return ServiceResult<NegocioVinculadoResponse>.Success(vinculacion.ToNegocioVinculadoResponse());
    }

    public async Task<ServiceResult<NegocioUsuarioVinculacionResponse>> LinkUserAsync(
        Guid negocioId,
        Guid userId,
        VincularUsuarioNegocioRequest request,
        Guid? linkedByUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || negocioId == Guid.Empty)
        {
            return ServiceResult<NegocioUsuarioVinculacionResponse>.Failure("validation_error", "Negocio y usuario son obligatorios.");
        }

        Negocio? negocio = await _negocioRepository.GetByIdAsync(negocioId, cancellationToken);
        if (negocio is null)
        {
            return ServiceResult<NegocioUsuarioVinculacionResponse>.Failure("not_found", "Negocio no encontrado.");
        }

        if (request.FechaFinUtc.HasValue && request.FechaInicioUtc.HasValue && request.FechaFinUtc.Value < request.FechaInicioUtc.Value)
        {
            return ServiceResult<NegocioUsuarioVinculacionResponse>.Failure("validation_error", "La fecha fin no puede ser anterior a la fecha inicio.");
        }

        DateTime now = DateTime.UtcNow;
        NegocioUsuarioVinculacion? existing = await _negocioUsuarioVinculacionRepository.GetByNegocioAndUserAsync(negocioId, userId, cancellationToken);

        if (existing is null)
        {
            existing = new NegocioUsuarioVinculacion
            {
                Id = Guid.NewGuid(),
                NegocioId = negocioId,
                UserId = userId,
                CreatedAtUtc = now,
                FechaInvitacionUtc = now
            };

            ApplyRequest(existing, request, linkedByUserId, now);
            existing.FechaAceptacionUtc ??= now;

            await _negocioUsuarioVinculacionRepository.AddAsync(existing, cancellationToken);
        }
        else
        {
            ApplyRequest(existing, request, linkedByUserId, now);
            _negocioUsuarioVinculacionRepository.Update(existing);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<NegocioUsuarioVinculacionResponse>.Success(existing.ToResponse());
    }

    public async Task<ServiceResult> UnlinkUserAsync(Guid negocioId, Guid userId, Guid? unlinkedByUserId = null, CancellationToken cancellationToken = default)
    {
        NegocioUsuarioVinculacion? existing = await _negocioUsuarioVinculacionRepository.GetByNegocioAndUserAsync(negocioId, userId, cancellationToken);
        if (existing is null || !existing.Activa)
        {
            return ServiceResult.Failure("not_found", "La vinculación usuario-negocio no existe o ya está inactiva.");
        }

        DateTime now = DateTime.UtcNow;
        existing.Activa = false;
        existing.UnlinkedByUserId = unlinkedByUserId;
        existing.RevokedAtUtc = now;
        existing.UpdatedAtUtc = now;
        _negocioUsuarioVinculacionRepository.Update(existing);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success();
    }

    private void ApplyRequest(
        NegocioUsuarioVinculacion vinculacion,
        VincularUsuarioNegocioRequest request,
        Guid? linkedByUserId,
        DateTime now)
    {
        vinculacion.TipoVinculacion = request.TipoVinculacion;
        vinculacion.TituloRelacion = Normalize(request.TituloRelacion);
        vinculacion.Activa = true;
        vinculacion.EsPrincipal = request.EsPrincipal;
        vinculacion.PuedeAccederBackoffice = request.PuedeAccederBackoffice;
        vinculacion.PuedeGestionarNegocio = request.PuedeGestionarNegocio;
        vinculacion.PuedeGestionarClientes = request.PuedeGestionarClientes;
        vinculacion.PuedeGestionarCampanas = request.PuedeGestionarCampanas;
        vinculacion.PuedeGestionarPuntos = request.PuedeGestionarPuntos;
        vinculacion.PuedeValidarTickets = request.PuedeValidarTickets;
        vinculacion.PuedeVerReportes = request.PuedeVerReportes;
        vinculacion.NotasInternas = Normalize(request.NotasInternas);
        vinculacion.OrigenVinculacion = Normalize(request.OrigenVinculacion);
        vinculacion.LinkedByUserId ??= linkedByUserId;
        vinculacion.UnlinkedByUserId = null;
        vinculacion.FechaAceptacionUtc ??= now;
        vinculacion.FechaInicioUtc = request.FechaInicioUtc ?? vinculacion.FechaInicioUtc ?? now;
        vinculacion.FechaFinUtc = request.FechaFinUtc;
        vinculacion.UpdatedAtUtc = now;
        vinculacion.RevokedAtUtc = null;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
