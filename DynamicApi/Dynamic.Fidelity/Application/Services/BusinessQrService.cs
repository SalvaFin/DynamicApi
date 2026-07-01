using System.Text;
using Dynamic.Fidelity.Application.Common;
using Dynamic.Fidelity.Application.Contracts.Services;
using Dynamic.Fidelity.Application.DTOs.Responses;
using Dynamic.Fidelity.Application.Options;
using Dynamic.Negocios.Application.Contracts.Repositories;
using Dynamic.Negocios.Domain.Entities;
using Dynamic.Negocios.Domain.Enums;
using Microsoft.Extensions.Options;
using QRCoder;

namespace Dynamic.Fidelity.Application.Services;

public class BusinessQrService : IBusinessQrService
{
    private readonly INegocioRepository _negocioRepository;
    private readonly INegocioUsuarioVinculacionRepository _vinculacionRepository;
    private readonly FidelityQrOptions _options;

    public BusinessQrService(
        INegocioRepository negocioRepository,
        INegocioUsuarioVinculacionRepository vinculacionRepository,
        IOptions<FidelityQrOptions> options)
    {
        _negocioRepository = negocioRepository;
        _vinculacionRepository = vinculacionRepository;
        _options = options.Value;
    }

    public async Task<ServiceResult<BusinessQrResponse>> GenerateAsync(
        Guid negocioId,
        Guid requesterUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        if (negocioId == Guid.Empty || requesterUserId == Guid.Empty)
        {
            return ServiceResult<BusinessQrResponse>.Failure(
                "validation_error",
                "El negocio y el usuario solicitante son obligatorios.");
        }

        Negocio? negocio = await _negocioRepository.GetByIdAsync(negocioId, cancellationToken);
        if (negocio is null || negocio.IsDeleted)
        {
            return ServiceResult<BusinessQrResponse>.Failure("not_found", "El negocio no existe.");
        }

        if (string.IsNullOrWhiteSpace(negocio.SlugPortal))
        {
            return ServiceResult<BusinessQrResponse>.Failure(
                "validation_error",
                "El negocio debe tener un slug público para generar su QR.");
        }

        if (!isAdmin)
        {
            ServiceResult authorization = await EnsureCanAccessBackofficeAsync(
                negocioId,
                requesterUserId,
                cancellationToken);

            if (!authorization.Succeeded)
            {
                return ServiceResult<BusinessQrResponse>.Failure(
                    authorization.ErrorCode ?? "forbidden",
                    authorization.ErrorMessage ?? "Sin permisos.");
            }
        }

        string normalizedSlug = negocio.SlugPortal.Trim().ToLowerInvariant();
        string publicUrl = BuildPublicUrl(normalizedSlug);
        string qrSvg = GenerateQrSvg(publicUrl);
        string qrDataUrl = $"data:image/svg+xml;base64,{Convert.ToBase64String(Encoding.UTF8.GetBytes(qrSvg))}";

        return ServiceResult<BusinessQrResponse>.Success(new BusinessQrResponse
        {
            NegocioId = negocio.Id,
            SlugPortal = normalizedSlug,
            PublicUrl = publicUrl,
            QrSvg = qrSvg,
            QrDataUrl = qrDataUrl
        });
    }

    private async Task<ServiceResult> EnsureCanAccessBackofficeAsync(
        Guid negocioId,
        Guid requesterUserId,
        CancellationToken cancellationToken)
    {
        NegocioUsuarioVinculacion? link = await _vinculacionRepository.GetByNegocioAndUserAsync(
            negocioId,
            requesterUserId,
            cancellationToken);

        if (link is null || !link.Activa || link.RevokedAtUtc.HasValue)
        {
            return ServiceResult.Failure("forbidden", "El usuario no está vinculado al negocio.");
        }

        DateTime now = DateTime.UtcNow;
        bool outsideDateWindow =
            (link.FechaInicioUtc.HasValue && link.FechaInicioUtc.Value > now) ||
            (link.FechaFinUtc.HasValue && link.FechaFinUtc.Value < now);

        if (outsideDateWindow)
        {
            return ServiceResult.Failure("forbidden", "La vinculación con el negocio no está activa.");
        }

        bool canAccess =
            link.PuedeAccederBackoffice ||
            link.PuedeGestionarNegocio ||
            link.TipoVinculacion is TipoVinculacionNegocioUsuario.Propietario or TipoVinculacionNegocioUsuario.Gerente;

        return canAccess
            ? ServiceResult.Success()
            : ServiceResult.Failure("forbidden", "El usuario no tiene acceso al backoffice del negocio.");
    }

    private string BuildPublicUrl(string slugPortal)
    {
        string baseUrl = _options.PublicBaseUrl.TrimEnd('/');
        string pathTemplate = string.IsNullOrWhiteSpace(_options.BusinessPagePathTemplate)
            ? "/negocios/{slug}"
            : _options.BusinessPagePathTemplate.Trim();

        string encodedSlug = Uri.EscapeDataString(slugPortal);
        string path = pathTemplate.Contains("{slug}", StringComparison.OrdinalIgnoreCase)
            ? pathTemplate.Replace("{slug}", encodedSlug, StringComparison.OrdinalIgnoreCase)
            : $"{pathTemplate.TrimEnd('/')}/{encodedSlug}";

        if (!path.StartsWith('/'))
        {
            path = $"/{path}";
        }

        return $"{baseUrl}{path}";
    }

    private static string GenerateQrSvg(string publicUrl)
    {
        using QRCodeGenerator generator = new();
        using QRCodeData qrCodeData = generator.CreateQrCode(publicUrl, QRCodeGenerator.ECCLevel.Q);
        SvgQRCode svgQrCode = new(qrCodeData);
        return svgQrCode.GetGraphic(20);
    }
}
