using System.Security.Cryptography;
using Dynamic.Fidelity.Application.Common;
using Dynamic.Fidelity.Application.Contracts.Repositories;
using Dynamic.Fidelity.Application.Contracts.Services;
using Dynamic.Fidelity.Application.DTOs.Responses;
using Dynamic.Fidelity.Application.Mappings;
using Dynamic.Fidelity.Application.Options;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Dynamic.Negocios.Application.Contracts.Repositories;
using Dynamic.Negocios.Domain.Entities;
using Dynamic.Negocios.Domain.Enums;
using Microsoft.Extensions.Options;
using QRCoder;

namespace Dynamic.Fidelity.Application.Services;

public class TicketQrService : ITicketQrService
{
    private readonly DynamicFidelityDbContext _dbContext;
    private readonly ITicketRepository _ticketRepository;
    private readonly IQrCampaignRepository _qrCampaignRepository;
    private readonly IPendingTicketAssignmentRepository _pendingTicketAssignmentRepository;
    private readonly FidelityQrOptions _fidelityQrOptions;
    private readonly IRegistrationRewardService _registrationRewardService;
    private readonly INegocioRepository _negocioRepository;
    private readonly INegocioUsuarioVinculacionRepository _negocioUsuarioVinculacionRepository;

    public TicketQrService(
        DynamicFidelityDbContext dbContext,
        ITicketRepository ticketRepository,
        IQrCampaignRepository qrCampaignRepository,
        IPendingTicketAssignmentRepository pendingTicketAssignmentRepository,
        IRegistrationRewardService registrationRewardService,
        INegocioRepository negocioRepository,
        INegocioUsuarioVinculacionRepository negocioUsuarioVinculacionRepository,
        IOptions<FidelityQrOptions> fidelityQrOptions)
    {
        _dbContext = dbContext;
        _ticketRepository = ticketRepository;
        _qrCampaignRepository = qrCampaignRepository;
        _pendingTicketAssignmentRepository = pendingTicketAssignmentRepository;
        _registrationRewardService = registrationRewardService;
        _negocioRepository = negocioRepository;
        _negocioUsuarioVinculacionRepository = negocioUsuarioVinculacionRepository;
        _fidelityQrOptions = fidelityQrOptions.Value;
    }

    public async Task<ServiceResult<TicketQrResponse>> GenerateTicketQrAsync(
        Guid negocioId,
        Guid ticketId,
        Guid requesterUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        if (negocioId == Guid.Empty || ticketId == Guid.Empty)
        {
            return ServiceResult<TicketQrResponse>.Failure("validation_error", "Negocio y ticket son obligatorios.");
        }

        ServiceResult authorization = await EnsureCanManageTicketQrAsync(negocioId, requesterUserId, isAdmin, cancellationToken);
        if (!authorization.Succeeded)
        {
            return ServiceResult<TicketQrResponse>.Failure(
                authorization.ErrorCode ?? "forbidden",
                authorization.ErrorMessage ?? "Sin permisos.");
        }

        Ticket? ticket = await _ticketRepository.GetByIdAsync(ticketId, cancellationToken);
        if (ticket is null || ticket.NegocioId != negocioId || !ticket.EsPlantilla)
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

    public async Task<ServiceResult<TicketQrScanResponse>> ScanTicketQrAsync(
        Guid userId,
        string qrToken,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(qrToken))
        {
            return ServiceResult<TicketQrScanResponse>.Failure("validation_error", "Usuario y qrToken son obligatorios.");
        }

        QrCampaign? campaign = await _qrCampaignRepository.GetByTokenAsync(qrToken.Trim(), cancellationToken);
        if (campaign is null || !campaign.WelcomeTicketTemplateId.HasValue)
        {
            return ServiceResult<TicketQrScanResponse>.Failure("not_found", "El QR no existe o no tiene un ticket asociado.");
        }

        bool alreadyClaimed = false;
        PendingTicketAssignment? existingAssignment =
            await _pendingTicketAssignmentRepository.GetByUserAndCampaignAsync(userId, campaign.Id, cancellationToken);

        if (existingAssignment?.Activated == true && existingAssignment.AssignedTicketId.HasValue)
        {
            alreadyClaimed = true;
        }

        Ticket? assignedTicket = await _registrationRewardService.ClaimTicketFromQrAsync(userId, qrToken, cancellationToken);
        if (assignedTicket is null)
        {
            return ServiceResult<TicketQrScanResponse>.Failure("validation_error", "No se ha podido vincular el ticket al usuario.");
        }

        return ServiceResult<TicketQrScanResponse>.Success(new TicketQrScanResponse
        {
            QrCampaignId = campaign.Id,
            NegocioId = campaign.NegocioId,
            UserId = userId,
            AlreadyClaimed = alreadyClaimed,
            Message = alreadyClaimed
                ? "El ticket ya estaba vinculado al usuario."
                : "El ticket se ha vinculado correctamente al usuario.",
            Ticket = assignedTicket.ToResponse()
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

    private async Task<ServiceResult> EnsureCanManageTicketQrAsync(
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
            return ServiceResult.Failure("forbidden", "La vinculación del usuario con el negocio no está activa.");
        }

        bool canManage =
            link.TipoVinculacion is TipoVinculacionNegocioUsuario.Propietario or TipoVinculacionNegocioUsuario.Gerente ||
            link.PuedeGestionarNegocio ||
            link.PuedeGestionarCampanas;

        return canManage
            ? ServiceResult.Success()
            : ServiceResult.Failure("forbidden", "El usuario no tiene permisos para generar QRs de tickets.");
    }
}
