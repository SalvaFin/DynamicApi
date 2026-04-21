using System.Security.Cryptography;
using Dynamic.Fidelity.Application.Common;
using Dynamic.Fidelity.Application.Contracts.Repositories;
using Dynamic.Fidelity.Application.Contracts.Services;
using Dynamic.Fidelity.Application.DTOs.Responses;
using Dynamic.Fidelity.Application.Options;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Microsoft.Extensions.Options;
using QRCoder;

namespace Dynamic.Fidelity.Application.Services;

public class TicketQrService : ITicketQrService
{
    private readonly DynamicFidelityDbContext _dbContext;
    private readonly ITicketRepository _ticketRepository;
    private readonly IQrCampaignRepository _qrCampaignRepository;
    private readonly FidelityQrOptions _fidelityQrOptions;

    public TicketQrService(
        DynamicFidelityDbContext dbContext,
        ITicketRepository ticketRepository,
        IQrCampaignRepository qrCampaignRepository,
        IOptions<FidelityQrOptions> fidelityQrOptions)
    {
        _dbContext = dbContext;
        _ticketRepository = ticketRepository;
        _qrCampaignRepository = qrCampaignRepository;
        _fidelityQrOptions = fidelityQrOptions.Value;
    }

    public async Task<ServiceResult<TicketQrResponse>> GenerateTicketQrAsync(Guid negocioId, Guid ticketId, CancellationToken cancellationToken = default)
    {
        if (negocioId == Guid.Empty || ticketId == Guid.Empty)
        {
            return ServiceResult<TicketQrResponse>.Failure("validation_error", "Negocio y ticket son obligatorios.");
        }

        Ticket? ticket = await _ticketRepository.GetByIdAsync(ticketId, cancellationToken);
        if (ticket is null || ticket.NegocioId != negocioId)
        {
            return ServiceResult<TicketQrResponse>.Failure("not_found", "El ticket no existe o no pertenece al negocio.");
        }

        DateTime now = DateTime.UtcNow;
        string qrToken = GenerateQrToken();
        string publicUrl = BuildPublicUrl(qrToken);

        QrCampaign qrCampaign = new()
        {
            Id = Guid.NewGuid(),
            NegocioId = negocioId,
            WelcomeTicketTemplateId = ticket.Id,
            Nombre = $"QR-{ticket.Nombre}",
            Token = qrToken,
            Descripcion = $"QR generado para el ticket {ticket.Nombre}.",
            LandingPath = BuildLandingPath(qrToken),
            Activa = true,
            Visible = true,
            UnSoloUsoPorUsuario = true,
            Expira = false,
            AvailableFromUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _qrCampaignRepository.AddAsync(qrCampaign, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<TicketQrResponse>.Success(new TicketQrResponse
        {
            QrCampaignId = qrCampaign.Id,
            NegocioId = negocioId,
            TicketId = ticket.Id,
            QrToken = qrToken,
            PublicUrl = publicUrl,
            QrSvg = GenerateQrSvg(publicUrl),
            CreatedAtUtc = now
        });
    }

    private string BuildPublicUrl(string qrToken)
    {
        string baseUrl = _fidelityQrOptions.PublicBaseUrl.TrimEnd('/');
        string registerPath = NormalizePath(_fidelityQrOptions.RegisterPath);
        return $"{baseUrl}{registerPath}?qr={Uri.EscapeDataString(qrToken)}";
    }

    private string BuildLandingPath(string qrToken)
        => $"{NormalizePath(_fidelityQrOptions.RegisterPath)}?qr={Uri.EscapeDataString(qrToken)}";

    private static string NormalizePath(string path)
    {
        string normalized = string.IsNullOrWhiteSpace(path) ? "/register" : path.Trim();
        return normalized.StartsWith('/') ? normalized : $"/{normalized}";
    }

    private static string GenerateQrToken()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string GenerateQrSvg(string publicUrl)
    {
        using QRCodeGenerator generator = new();
        using QRCodeData qrCodeData = generator.CreateQrCode(publicUrl, QRCodeGenerator.ECCLevel.Q);
        SvgQRCode svgQrCode = new(qrCodeData);
        return svgQrCode.GetGraphic(20);
    }
}
