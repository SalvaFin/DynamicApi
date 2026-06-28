using System.Security.Cryptography;
using System.Text;
using Dynamic.Fidelity.Application.Common;
using Dynamic.Fidelity.Application.Contracts.Repositories;
using Dynamic.Fidelity.Application.Contracts.Services;
using Dynamic.Fidelity.Application.DTOs.Requests;
using Dynamic.Fidelity.Application.DTOs.Responses;
using Dynamic.Fidelity.Application.Mappings;
using Dynamic.Fidelity.Application.Options;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Domain.Enums;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Dynamic.Negocios.Application.Contracts.Repositories;
using Dynamic.Negocios.Domain.Entities;
using Dynamic.Negocios.Domain.Enums;
using Microsoft.Extensions.Options;
using QRCoder;

namespace Dynamic.Fidelity.Application.Services;

public class TicketQrService : ITicketQrService
{
    private const string AssignedTicketTokenPrefix = "utk1";

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
        Negocio? negocio = await _negocioRepository.GetByIdAsync(negocioId, cancellationToken);
        if (negocio is null || negocio.IsDeleted || string.IsNullOrWhiteSpace(negocio.SlugPortal))
        {
            return ServiceResult<TicketQrResponse>.Failure(
                "not_found",
                "No se ha podido resolver el slug público del negocio para generar el QR.");
        }

        string publicUrl = BuildPublicUrl(negocio.SlugPortal, qrToken);

        QrCampaign qrCampaign = new()
        {
            Id = Guid.NewGuid(),
            NegocioId = negocioId,
            WelcomeTicketTemplateId = ticket.Id,
            Nombre = $"QR-{ticket.Nombre}",
            Token = qrToken,
            Descripcion = $"QR generado para el ticket {ticket.Nombre}.",
            LandingPath = BuildLandingPath(negocio.SlugPortal, qrToken),
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

    public async Task<ServiceResult<TicketQrLookupResponse>> GetTicketByQrAsync(
        string slugPortal,
        string qrToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slugPortal) || string.IsNullOrWhiteSpace(qrToken))
        {
            return ServiceResult<TicketQrLookupResponse>.Failure("validation_error", "El slug del negocio y el qr son obligatorios.");
        }

        string normalizedSlug = slugPortal.Trim().Trim('/').ToLowerInvariant();
        QrCampaign? campaign = await _qrCampaignRepository.GetByTokenAsync(qrToken.Trim(), cancellationToken);
        if (!IsCampaignValid(campaign) || !campaign!.WelcomeTicketTemplateId.HasValue)
        {
            return ServiceResult<TicketQrLookupResponse>.Failure("not_found", "El QR no existe o no tiene un ticket asociado.");
        }

        Negocio? negocio = await _negocioRepository.GetByPublicIdentifierAsync(normalizedSlug, cancellationToken);
        if (negocio is null ||
            negocio.IsDeleted ||
            negocio.Id != campaign.NegocioId)
        {
            return ServiceResult<TicketQrLookupResponse>.Failure("not_found", "El QR no pertenece al negocio indicado.");
        }

        Ticket? ticket = await _ticketRepository.GetByIdAsync(campaign.WelcomeTicketTemplateId.Value, cancellationToken);
        if (ticket is null || ticket.NegocioId != campaign.NegocioId || !ticket.EsPlantilla || ticket.UserId.HasValue)
        {
            return ServiceResult<TicketQrLookupResponse>.Failure("not_found", "El ticket asociado al QR ya no está disponible.");
        }

        string publicUrl = BuildPublicUrl(negocio.SlugPortal, campaign.Token);
        string landingPath = BuildLandingPath(negocio.SlugPortal, campaign.Token);

        return ServiceResult<TicketQrLookupResponse>.Success(new TicketQrLookupResponse
        {
            QrCampaignId = campaign.Id,
            NegocioId = negocio.Id,
            SlugPortal = negocio.SlugPortal,
            TicketId = ticket.Id,
            QrToken = campaign.Token,
            PublicUrl = publicUrl,
            LandingPath = landingPath,
            Ticket = ticket.ToResponse()
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
        if (!IsCampaignValid(campaign) || !campaign!.WelcomeTicketTemplateId.HasValue)
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

    public async Task<ServiceResult<AssignedTicketQrResponse>> GenerateAssignedTicketQrAsync(
        Guid ticketId,
        Guid requesterUserId,
        CancellationToken cancellationToken = default)
    {
        if (ticketId == Guid.Empty || requesterUserId == Guid.Empty)
        {
            return ServiceResult<AssignedTicketQrResponse>.Failure("validation_error", "Ticket y usuario son obligatorios.");
        }

        Ticket? ticket = await _ticketRepository.GetByIdAsync(ticketId, cancellationToken);
        if (ticket is null || ticket.UserId != requesterUserId || ticket.EsPlantilla)
        {
            return ServiceResult<AssignedTicketQrResponse>.Failure("not_found", "Ticket no encontrado.");
        }

        ServiceResult availability = EnsureTicketCanBeUsed(ticket);
        if (!availability.Succeeded)
        {
            return ServiceResult<AssignedTicketQrResponse>.Failure(
                availability.ErrorCode ?? "conflict",
                availability.ErrorMessage ?? "El ticket no está disponible para canje.");
        }

        string qrToken = BuildAssignedTicketToken(ticket.Id, requesterUserId);
        DateTime now = DateTime.UtcNow;

        return ServiceResult<AssignedTicketQrResponse>.Success(new AssignedTicketQrResponse
        {
            NegocioId = ticket.NegocioId,
            TicketId = ticket.Id,
            UserId = requesterUserId,
            TicketCode = ticket.CodigoVisible,
            QrToken = qrToken,
            Payload = qrToken,
            QrSvg = GenerateQrSvg(qrToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = ticket.ExpiresAtUtc
        });
    }

    public async Task<ServiceResult<ValidateTicketQrResponse>> ValidateAssignedTicketQrAsync(
        Guid negocioId,
        Guid employeeUserId,
        bool isAdmin,
        ValidateTicketQrRequest request,
        CancellationToken cancellationToken = default)
    {
        if (negocioId == Guid.Empty ||
            employeeUserId == Guid.Empty ||
            (string.IsNullOrWhiteSpace(request.QrToken) && string.IsNullOrWhiteSpace(request.TicketCode)))
        {
            return ServiceResult<ValidateTicketQrResponse>.Failure("validation_error", "Negocio, empleado y qrToken o ticketCode son obligatorios.");
        }

        ServiceResult authorization = await EnsureCanValidateTicketAsync(negocioId, employeeUserId, isAdmin, cancellationToken);
        if (!authorization.Succeeded)
        {
            return ServiceResult<ValidateTicketQrResponse>.Failure(
                authorization.ErrorCode ?? "forbidden",
                authorization.ErrorMessage ?? "Sin permisos para validar tickets.");
        }

        ServiceResult<Ticket> ticketResult = await ResolveTicketForValidationAsync(negocioId, request, cancellationToken);
        if (!ticketResult.Succeeded || ticketResult.Data is null)
        {
            return ServiceResult<ValidateTicketQrResponse>.Failure(
                ticketResult.ErrorCode ?? "validation_error",
                ticketResult.ErrorMessage ?? "No se ha podido resolver el ticket.");
        }

        Ticket ticket = ticketResult.Data;
        ServiceResult availability = EnsureTicketCanBeUsed(ticket);
        if (!availability.Succeeded)
        {
            return ServiceResult<ValidateTicketQrResponse>.Failure(
                availability.ErrorCode ?? "conflict",
                availability.ErrorMessage ?? "El ticket no está disponible para canje.");
        }

        ServiceResult minimumSpendValidation = EnsureMinimumSpendIsMet(ticket, request.PurchaseAmount);
        if (!minimumSpendValidation.Succeeded)
        {
            return ServiceResult<ValidateTicketQrResponse>.Failure(
                minimumSpendValidation.ErrorCode ?? "conflict",
                minimumSpendValidation.ErrorMessage ?? "No se cumple el importe minimo del ticket.");
        }

        DateTime now = DateTime.UtcNow;
        ticket.UsosConsumidos++;

        int? usageLimit = ticket.MaxUsosPorCliente ?? (ticket.EsDeUnSoloUso ? 1 : null);
        if (ticket.EsDeUnSoloUso || (usageLimit.HasValue && ticket.UsosConsumidos >= usageLimit.Value))
        {
            ticket.Usado = true;
            ticket.UsedAtUtc = now;
        }

        ticket.UsedInStoreReference = Normalize(request.StoreReference);
        ticket.UsedByEmployeeReference = employeeUserId.ToString("D");
        ticket.UpdatedAtUtc = now;

        _ticketRepository.Update(ticket);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<ValidateTicketQrResponse>.Success(new ValidateTicketQrResponse
        {
            NegocioId = negocioId,
            TicketId = ticket.Id,
            UserId = ticket.UserId!.Value,
            ValidatedByUserId = employeeUserId,
            Used = ticket.Usado,
            UsosConsumidos = ticket.UsosConsumidos,
            UsedAtUtc = ticket.UsedAtUtc,
            PurchaseAmount = NormalizeAmount(request.PurchaseAmount),
            DiscountAmount = CalculateDiscountAmount(ticket, request.PurchaseAmount),
            FinalAmount = CalculateFinalAmount(ticket, request.PurchaseAmount),
            MinimumSpendSatisfied = ResolveMinimumSpendSatisfied(ticket, request.PurchaseAmount),
            Message = ticket.Usado
                ? "El ticket se ha validado y marcado como usado."
                : "El uso del ticket se ha validado correctamente.",
            Ticket = ticket.ToValidatedResponse()
        });
    }

    private static bool IsCampaignValid(QrCampaign? campaign)
    {
        if (campaign is null || !campaign.Activa)
        {
            return false;
        }

        DateTime now = DateTime.UtcNow;

        if (campaign.AvailableFromUtc.HasValue && campaign.AvailableFromUtc.Value > now)
        {
            return false;
        }

        if (campaign.Expira && campaign.ExpiresAtUtc.HasValue && campaign.ExpiresAtUtc.Value < now)
        {
            return false;
        }

        return true;
    }

    private ServiceResult<AssignedTicketTokenPayload> TryReadAssignedTicketToken(string input)
    {
        string token = ExtractQrToken(input);
        string[] parts = token.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3 || parts[0] != AssignedTicketTokenPrefix)
        {
            return ServiceResult<AssignedTicketTokenPayload>.Failure("validation_error", "El QR no corresponde a un ticket de usuario.");
        }

        string expectedSignature = Sign(parts[1]);
        if (!FixedTimeEquals(parts[2], expectedSignature))
        {
            return ServiceResult<AssignedTicketTokenPayload>.Failure("validation_error", "La firma del QR no es válida.");
        }

        string payload;
        try
        {
            payload = Encoding.UTF8.GetString(DecodeBase64Url(parts[1]));
        }
        catch (FormatException)
        {
            return ServiceResult<AssignedTicketTokenPayload>.Failure("validation_error", "El contenido del QR no es válido.");
        }

        string[] values = payload.Split(':', StringSplitOptions.TrimEntries);
        if (values.Length != 2 ||
            !Guid.TryParseExact(values[0], "N", out Guid ticketId) ||
            !Guid.TryParseExact(values[1], "N", out Guid userId))
        {
            return ServiceResult<AssignedTicketTokenPayload>.Failure("validation_error", "El contenido del QR no es válido.");
        }

        return ServiceResult<AssignedTicketTokenPayload>.Success(new AssignedTicketTokenPayload(ticketId, userId));
    }

    private async Task<ServiceResult<Ticket>> ResolveTicketForValidationAsync(
        Guid negocioId,
        ValidateTicketQrRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.QrToken))
        {
            ServiceResult<AssignedTicketTokenPayload> tokenResult = TryReadAssignedTicketToken(request.QrToken);
            if (!tokenResult.Succeeded || tokenResult.Data is null)
            {
                return ServiceResult<Ticket>.Failure(
                    tokenResult.ErrorCode ?? "validation_error",
                    tokenResult.ErrorMessage ?? "QR de ticket no válido.");
            }

            Ticket? ticket = await _ticketRepository.GetByIdAsync(tokenResult.Data.TicketId, cancellationToken);
            if (ticket is null ||
                ticket.UserId != tokenResult.Data.UserId ||
                ticket.NegocioId != negocioId ||
                ticket.EsPlantilla)
            {
                return ServiceResult<Ticket>.Failure("not_found", "El ticket no existe o no pertenece a este negocio.");
            }

            return ServiceResult<Ticket>.Success(ticket);
        }

        string ticketCode = request.TicketCode!.Trim();
        Ticket? ticketByCode = await _ticketRepository.GetAssignedByVisibleCodeAsync(
            negocioId,
            ticketCode,
            cancellationToken);

        return ticketByCode is null
            ? ServiceResult<Ticket>.Failure("not_found", "No se ha encontrado ningún ticket activo con ese código en este negocio.")
            : ServiceResult<Ticket>.Success(ticketByCode);
    }

    private string ExtractQrToken(string input)
    {
        string trimmed = input.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri))
        {
            return trimmed;
        }

        string query = uri.Query.TrimStart('?');
        if (string.IsNullOrWhiteSpace(query))
        {
            return trimmed;
        }

        string preferredName = string.IsNullOrWhiteSpace(_fidelityQrOptions.TicketQrQueryParameterName)
            ? "ticketQr"
            : _fidelityQrOptions.TicketQrQueryParameterName.Trim();

        foreach (string parameter in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] keyValue = parameter.Split('=', 2);
            if (keyValue.Length == 2 &&
                string.Equals(Uri.UnescapeDataString(keyValue[0]), preferredName, StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(keyValue[1]);
            }
        }

        return trimmed;
    }

    private string BuildAssignedTicketToken(Guid ticketId, Guid userId)
    {
        string payload = EncodeBase64Url(Encoding.UTF8.GetBytes($"{ticketId:N}:{userId:N}"));
        return $"{AssignedTicketTokenPrefix}.{payload}.{Sign(payload)}";
    }

    private string Sign(string payload)
    {
        byte[] key = Encoding.UTF8.GetBytes(_fidelityQrOptions.TicketSigningSecret);
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
        using HMACSHA256 hmac = new(key);
        return EncodeBase64Url(hmac.ComputeHash(payloadBytes));
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        byte[] leftBytes = Encoding.UTF8.GetBytes(left);
        byte[] rightBytes = Encoding.UTF8.GetBytes(right);
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static string EncodeBase64Url(byte[] value)
        => Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static byte[] DecodeBase64Url(string value)
    {
        string base64 = value
            .Replace('-', '+')
            .Replace('_', '/');

        int padding = base64.Length % 4;
        if (padding > 0)
        {
            base64 = base64.PadRight(base64.Length + 4 - padding, '=');
        }

        return Convert.FromBase64String(base64);
    }

    private static ServiceResult EnsureTicketCanBeShownAsQr(Ticket ticket)
    {
        if (!ticket.Activo)
        {
            return ServiceResult.Failure("conflict", "El ticket no está activo.");
        }

        if (ticket.Usado)
        {
            return ServiceResult.Failure("conflict", "El ticket ya se ha usado.");
        }

        DateTime now = DateTime.UtcNow;
        if (ticket.AvailableFromUtc.HasValue && ticket.AvailableFromUtc.Value > now)
        {
            return ServiceResult.Failure("conflict", "El ticket todavía no está disponible.");
        }

        if (ticket.ExpiresAtUtc <= now)
        {
            return ServiceResult.Failure("conflict", "El ticket ha expirado.");
        }

        return ServiceResult.Success();
    }

    private static ServiceResult EnsureTicketCanBeUsed(Ticket ticket)
    {
        ServiceResult availability = EnsureTicketCanBeShownAsQr(ticket);
        if (!availability.Succeeded)
        {
            return availability;
        }

        int? usageLimit = ticket.MaxUsosPorCliente ?? (ticket.EsDeUnSoloUso ? 1 : null);
        if (usageLimit.HasValue && ticket.UsosConsumidos >= usageLimit.Value)
        {
            return ServiceResult.Failure("conflict", "El ticket ya ha alcanzado el límite de usos.");
        }

        return ServiceResult.Success();
    }

    private static ServiceResult EnsureMinimumSpendIsMet(Ticket ticket, decimal? purchaseAmount)
    {
        if (!ticket.GastoMinimoRequerido.HasValue || ticket.GastoMinimoRequerido.Value <= 0)
        {
            return ServiceResult.Success();
        }

        if (!purchaseAmount.HasValue)
        {
            return ServiceResult.Failure(
                "validation_error",
                "Este ticket requiere informar el importe de la cuenta para comprobar el gasto minimo.");
        }

        decimal normalizedPurchaseAmount = NormalizeAmount(purchaseAmount).GetValueOrDefault();
        decimal minimumSpend = NormalizeAmount(ticket.GastoMinimoRequerido).GetValueOrDefault();
        if (normalizedPurchaseAmount < minimumSpend)
        {
            return ServiceResult.Failure(
                "conflict",
                $"El ticket requiere un gasto minimo de {minimumSpend:0.00} y la cuenta indicada es de {normalizedPurchaseAmount:0.00}.");
        }

        return ServiceResult.Success();
    }

    private static bool? ResolveMinimumSpendSatisfied(Ticket ticket, decimal? purchaseAmount)
    {
        if (!ticket.GastoMinimoRequerido.HasValue || ticket.GastoMinimoRequerido.Value <= 0)
        {
            return null;
        }

        return purchaseAmount.HasValue &&
            NormalizeAmount(purchaseAmount).GetValueOrDefault() >= NormalizeAmount(ticket.GastoMinimoRequerido).GetValueOrDefault();
    }

    private static decimal? CalculateDiscountAmount(Ticket ticket, decimal? purchaseAmount)
    {
        if (!purchaseAmount.HasValue)
        {
            return null;
        }

        decimal normalizedPurchaseAmount = NormalizeAmount(purchaseAmount).GetValueOrDefault();
        if (normalizedPurchaseAmount <= 0)
        {
            return 0;
        }

        decimal discount = ticket.Tipo switch
        {
            TipoTicket.DescuentoPorcentual => normalizedPurchaseAmount * (ticket.DescuentoPorcentaje ?? ticket.Valor) / 100,
            TipoTicket.DescuentoImporteFijo => ticket.DescuentoImporteFijo ?? ticket.Valor,
            _ => 0
        };

        discount = Math.Clamp(discount, 0, normalizedPurchaseAmount);
        return NormalizeAmount(discount);
    }

    private static decimal? CalculateFinalAmount(Ticket ticket, decimal? purchaseAmount)
    {
        if (!purchaseAmount.HasValue)
        {
            return null;
        }

        decimal normalizedPurchaseAmount = NormalizeAmount(purchaseAmount).GetValueOrDefault();
        decimal discountAmount = CalculateDiscountAmount(ticket, normalizedPurchaseAmount).GetValueOrDefault();
        return NormalizeAmount(normalizedPurchaseAmount - discountAmount);
    }

    private static decimal? NormalizeAmount(decimal? amount)
        => amount.HasValue
            ? decimal.Round(amount.Value, 2, MidpointRounding.AwayFromZero)
            : null;

    private string BuildPublicUrl(string slugPortal, string qrToken)
    {
        string baseUrl = _fidelityQrOptions.PublicBaseUrl.TrimEnd('/');
        string landingPath = BuildLandingPath(slugPortal, qrToken);
        return $"{baseUrl}{landingPath}";
    }

    private string BuildLandingPath(string slugPortal, string qrToken)
    {
        string pathTemplate = NormalizePathTemplate(_fidelityQrOptions.BusinessLandingPathTemplate);
        string resolvedPath = pathTemplate
            .Replace("{slug}", Uri.EscapeDataString(slugPortal.Trim().ToLowerInvariant()), StringComparison.OrdinalIgnoreCase)
            .Replace("{qrToken}", Uri.EscapeDataString(qrToken), StringComparison.OrdinalIgnoreCase);

        if (resolvedPath.Contains("{slug}", StringComparison.OrdinalIgnoreCase))
        {
            resolvedPath = resolvedPath.Replace("{slug}", Uri.EscapeDataString(slugPortal.Trim().ToLowerInvariant()), StringComparison.Ordinal);
        }

        if (resolvedPath.Contains("{qrToken}", StringComparison.OrdinalIgnoreCase))
        {
            return resolvedPath.Replace("{qrToken}", Uri.EscapeDataString(qrToken), StringComparison.Ordinal);
        }

        string queryParameterName = string.IsNullOrWhiteSpace(_fidelityQrOptions.QrQueryParameterName)
            ? "qr"
            : _fidelityQrOptions.QrQueryParameterName.Trim();

        char querySeparator = resolvedPath.Contains('?') ? '&' : '?';
        return $"{resolvedPath}{querySeparator}{Uri.EscapeDataString(queryParameterName)}={Uri.EscapeDataString(qrToken)}";
    }

    private static string NormalizePathTemplate(string pathTemplate)
    {
        string normalized = string.IsNullOrWhiteSpace(pathTemplate) ? "/portal/tickets" : pathTemplate.Trim();
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

    private async Task<ServiceResult> EnsureCanValidateTicketAsync(
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

        bool canValidate =
            link.TipoVinculacion is TipoVinculacionNegocioUsuario.Propietario or TipoVinculacionNegocioUsuario.Gerente ||
            link.PuedeGestionarNegocio ||
            link.PuedeValidarTickets;

        return link.PuedeAccederBackoffice && canValidate
            ? ServiceResult.Success()
            : ServiceResult.Failure("forbidden", "El usuario no tiene permisos para validar tickets.");
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record AssignedTicketTokenPayload(Guid TicketId, Guid UserId);
}
