using Dynamic.Negocios.Application.Common;
using Dynamic.Negocios.Application.Contracts.Repositories;
using Dynamic.Negocios.Application.Contracts.Services;
using Dynamic.Negocios.Application.DTOs.Requests;
using Dynamic.Negocios.Application.DTOs.Responses;
using Dynamic.Negocios.Application.Mappings;
using Dynamic.Negocios.Domain.Entities;
using Dynamic.Negocios.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Dynamic.Negocios.Application.Services;

public class NegocioService : INegocioService
{
    private readonly DynamicNegociosDbContext _dbContext;
    private readonly INegocioRepository _negocioRepository;
    private readonly INegocioUsuarioVinculacionRepository _negocioUsuarioVinculacionRepository;
    private readonly INegocioMediaStorageService _negocioMediaStorageService;
    private readonly ILogger<NegocioService> _logger;

    public NegocioService(
        DynamicNegociosDbContext dbContext,
        INegocioRepository negocioRepository,
        INegocioUsuarioVinculacionRepository negocioUsuarioVinculacionRepository,
        INegocioMediaStorageService negocioMediaStorageService,
        ILogger<NegocioService> logger)
    {
        _dbContext = dbContext;
        _negocioRepository = negocioRepository;
        _negocioUsuarioVinculacionRepository = negocioUsuarioVinculacionRepository;
        _negocioMediaStorageService = negocioMediaStorageService;
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

        if (request is ActualizarNegocioMultipartRequest multipartRequest)
        {
            ServiceResult imageUploadResult = await ApplyUploadedImagesAsync(id, multipartRequest, cancellationToken);
            if (!imageUploadResult.Succeeded)
            {
                return ServiceResult<NegocioResponse>.Failure(
                    imageUploadResult.ErrorCode ?? "validation_error",
                    imageUploadResult.ErrorMessage ?? "No se pudieron procesar las im\u00e1genes del negocio.");
            }
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

        if (request.RatioConversionEurosAPuntos.HasValue && request.RatioConversionEurosAPuntos.Value <= 0)
        {
            return "El ratio de conversión de euros a puntos debe ser mayor que cero.";
        }

        if (!string.IsNullOrWhiteSpace(request.ClaveMaestraLocal) &&
            (request.ClaveMaestraLocal.Length != 4 || request.ClaveMaestraLocal.Any(character => !char.IsDigit(character))))
        {
            return "La clave maestra del local debe tener exactamente 4 dígitos.";
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

    private async Task<ServiceResult> ApplyUploadedImagesAsync(
        Guid negocioId,
        ActualizarNegocioMultipartRequest request,
        CancellationToken cancellationToken)
    {
        ServiceResult<string>? uploadResult;

        uploadResult = await UploadIfPresentAsync(negocioId, "logo-principal", request.LogoPrincipalFile, cancellationToken);
        if (uploadResult is not null)
        {
            if (!uploadResult.Succeeded)
            {
                return ServiceResult.Failure(uploadResult.ErrorCode ?? "validation_error", uploadResult.ErrorMessage ?? "No se pudo subir el logo principal.");
            }

            request.LogoPrincipalUrl = uploadResult.Data;
        }

        uploadResult = await UploadIfPresentAsync(negocioId, "logo-secundario", request.LogoSecundarioFile, cancellationToken);
        if (uploadResult is not null)
        {
            if (!uploadResult.Succeeded)
            {
                return ServiceResult.Failure(uploadResult.ErrorCode ?? "validation_error", uploadResult.ErrorMessage ?? "No se pudo subir el logo secundario.");
            }

            request.LogoSecundarioUrl = uploadResult.Data;
        }

        uploadResult = await UploadIfPresentAsync(negocioId, "icono", request.IconoFile, cancellationToken);
        if (uploadResult is not null)
        {
            if (!uploadResult.Succeeded)
            {
                return ServiceResult.Failure(uploadResult.ErrorCode ?? "validation_error", uploadResult.ErrorMessage ?? "No se pudo subir el icono.");
            }

            request.IconoUrl = uploadResult.Data;
        }

        uploadResult = await UploadIfPresentAsync(negocioId, "hero", request.ImagenHeroFile, cancellationToken);
        if (uploadResult is not null)
        {
            if (!uploadResult.Succeeded)
            {
                return ServiceResult.Failure(uploadResult.ErrorCode ?? "validation_error", uploadResult.ErrorMessage ?? "No se pudo subir la imagen hero.");
            }

            request.ImagenHeroUrl = uploadResult.Data;
        }

        uploadResult = await UploadIfPresentAsync(negocioId, "cover", request.ImagenCoverFile, cancellationToken);
        if (uploadResult is not null)
        {
            if (!uploadResult.Succeeded)
            {
                return ServiceResult.Failure(uploadResult.ErrorCode ?? "validation_error", uploadResult.ErrorMessage ?? "No se pudo subir la imagen cover.");
            }

            request.ImagenCoverUrl = uploadResult.Data;
        }

        uploadResult = await UploadIfPresentAsync(negocioId, "mobile", request.ImagenMobileFile, cancellationToken);
        if (uploadResult is not null)
        {
            if (!uploadResult.Succeeded)
            {
                return ServiceResult.Failure(uploadResult.ErrorCode ?? "validation_error", uploadResult.ErrorMessage ?? "No se pudo subir la imagen mobile.");
            }

            request.ImagenMobileUrl = uploadResult.Data;
        }

        if (request.GaleriaImagenesFiles is { Count: > 0 })
        {
            List<string> galleryUrls = [];

            foreach (var galleryFile in request.GaleriaImagenesFiles.Where(file => file is not null))
            {
                ServiceResult<string> galleryUpload =
                    await _negocioMediaStorageService.SaveImageAsync(negocioId, "gallery", galleryFile, cancellationToken);

                if (!galleryUpload.Succeeded || string.IsNullOrWhiteSpace(galleryUpload.Data))
                {
                    return ServiceResult.Failure(
                        galleryUpload.ErrorCode ?? "validation_error",
                        galleryUpload.ErrorMessage ?? "No se pudo subir una imagen de la galer\u00eda.");
                }

                galleryUrls.Add(galleryUpload.Data);
            }

            request.GaleriaImagenesJson = JsonSerializer.Serialize(galleryUrls);
        }

        return ServiceResult.Success();
    }

    private async Task<ServiceResult<string>?> UploadIfPresentAsync(
        Guid negocioId,
        string imageSlot,
        Microsoft.AspNetCore.Http.IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return null;
        }

        return await _negocioMediaStorageService.SaveImageAsync(negocioId, imageSlot, file, cancellationToken);
    }
}
