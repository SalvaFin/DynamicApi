using System.Security.Claims;
using Dynamic.Fidelity.Application.Common;
using Dynamic.Fidelity.Application.Contracts.Services;
using Dynamic.Fidelity.Application.DTOs.Requests;
using Dynamic.Fidelity.Application.DTOs.Responses;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Dynamic.Negocios.Domain.Entities;
using Dynamic.Negocios.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace Dynamic.Fidelity.Controllers;

[ApiController]
[Authorize]
[Route("api/users/me")]
public class UserPortalController : ControllerBase
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly DynamicFidelityDbContext _fidelityDbContext;
    private readonly DynamicNegociosDbContext _negociosDbContext;
    private readonly ITicketQrService _ticketQrService;
    private readonly IUserCodeDirectoryService _userCodeDirectoryService;

    public UserPortalController(
        DynamicFidelityDbContext fidelityDbContext,
        DynamicNegociosDbContext negociosDbContext,
        ITicketQrService ticketQrService,
        IUserCodeDirectoryService userCodeDirectoryService)
    {
        _fidelityDbContext = fidelityDbContext;
        _negociosDbContext = negociosDbContext;
        _ticketQrService = ticketQrService;
        _userCodeDirectoryService = userCodeDirectoryService;
    }

    [HttpGet("qr")]
    public async Task<IActionResult> GetMyQr(CancellationToken cancellationToken)
    {
        Guid? userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        string userCode = await _userCodeDirectoryService.EnsureUserCodeAsync(userId.Value, cancellationToken);
        string payload = userId.Value.ToString("D");

        using QRCodeGenerator generator = new();
        using QRCodeData qrCodeData = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        SvgQRCode svgQrCode = new(qrCodeData);

        return Ok(new UserQrResponse
        {
            UserId = userId.Value,
            UserCode = userCode,
            Payload = payload,
            QrSvg = svgQrCode.GetGraphic(20)
        });
    }

    [HttpGet("businesses")]
    public async Task<IActionResult> GetMyBusinesses(CancellationToken cancellationToken)
    {
        Guid? userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        DateTime now = DateTime.UtcNow;

        var ticketStats = await _fidelityDbContext.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.UserId == userId.Value)
            .GroupBy(ticket => ticket.NegocioId)
            .Select(group => new
            {
                NegocioId = group.Key,
                TicketsTotales = group.Count(),
                TicketsActivos = group.Count(ticket => ticket.Activo && !ticket.Usado && ticket.ExpiresAtUtc > now),
                LastTicketActivityUtc = group.Max(ticket => ticket.UpdatedAtUtc)
            })
            .ToListAsync(cancellationToken);

        var pointStats = await _fidelityDbContext.Points
            .AsNoTracking()
            .Where(points => points.UserId == userId.Value)
            .Select(points => new
            {
                points.NegocioId,
                PuntosActuales = points.CurrentBalance,
                LastPointsActivityUtc = points.LastMovementAtUtc ?? points.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var activeLinks = await _negociosDbContext.NegociosUsuariosVinculaciones
            .AsNoTracking()
            .Where(link =>
                link.UserId == userId.Value &&
                link.Activa &&
                !link.RevokedAtUtc.HasValue &&
                (!link.FechaFinUtc.HasValue || link.FechaFinUtc.Value > now))
            .Select(link => new
            {
                link.NegocioId,
                link.TipoVinculacion,
                LinkActivityUtc = link.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        Guid[] explicitlyUnlinkedNegocioIds = await _negociosDbContext.NegociosUsuariosVinculaciones
            .AsNoTracking()
            .Where(link =>
                link.UserId == userId.Value &&
                link.TipoVinculacion == Dynamic.Negocios.Domain.Enums.TipoVinculacionNegocioUsuario.Cliente &&
                (!link.Activa || link.RevokedAtUtc.HasValue ||
                 (link.FechaFinUtc.HasValue && link.FechaFinUtc.Value <= now)))
            .Select(link => link.NegocioId)
            .ToArrayAsync(cancellationToken);

        HashSet<Guid> explicitlyUnlinkedNegocioIdSet = explicitlyUnlinkedNegocioIds.ToHashSet();

        Guid[] negocioIds = ticketStats.Select(ticket => ticket.NegocioId)
            .Concat(pointStats.Select(points => points.NegocioId))
            .Concat(activeLinks.Select(link => link.NegocioId))
            .Distinct()
            .Where(negocioId => !explicitlyUnlinkedNegocioIdSet.Contains(negocioId))
            .ToArray();

        if (negocioIds.Length == 0)
        {
            return Ok(Array.Empty<UserPortalBusinessResponse>());
        }

        HashSet<Guid> negocioIdSet = negocioIds.ToHashSet();
        Dictionary<Guid, Negocio> negocios = (await _negociosDbContext.Negocios
                .AsNoTracking()
                .Where(negocio => !negocio.IsDeleted)
                .ToListAsync(cancellationToken))
            .Where(negocio => negocioIdSet.Contains(negocio.Id))
            .ToDictionary(negocio => negocio.Id);

        IReadOnlyCollection<UserPortalBusinessResponse> response = negocioIds
            .Where(negocios.ContainsKey)
            .Select(negocioId =>
            {
                Negocio negocio = negocios[negocioId];
                var ticket = ticketStats.FirstOrDefault(item => item.NegocioId == negocioId);
                var points = pointStats.FirstOrDefault(item => item.NegocioId == negocioId);
                var link = activeLinks.FirstOrDefault(item => item.NegocioId == negocioId);

                return new UserPortalBusinessResponse
                {
                    Id = negocio.Id,
                    Nombre = negocio.NombreComercial,
                    Slug = negocio.SlugPortal,
                    LogoUrl = negocio.LogoPrincipalUrl,
                    IconoUrl = negocio.IconoUrl,
                    ImagenCoverUrl = negocio.ImagenCoverUrl,
                    Categoria = negocio.CategoriaPrincipal,
                    Ciudad = negocio.Ciudad,
                    Provincia = negocio.Provincia,
                    Activo = negocio.Activo,
                    PublicadoPortal = negocio.PublicadoPortal,
                    PuntosActuales = points?.PuntosActuales ?? 0,
                    TicketsActivos = ticket?.TicketsActivos ?? 0,
                    TicketsTotales = ticket?.TicketsTotales ?? 0,
                    LinkedFromTickets = ticket is not null,
                    LinkedFromPoints = points is not null,
                    LinkedFromVinculacion = link is not null,
                    TipoVinculacion = link?.TipoVinculacion.ToString(),
                    FechaUltimaActividadUtc = MaxDate(ticket?.LastTicketActivityUtc, points?.LastPointsActivityUtc, link?.LinkActivityUtc)
                };
            })
            .OrderByDescending(negocio => negocio.FechaUltimaActividadUtc)
            .ThenBy(negocio => negocio.Nombre)
            .ToArray();

        return Ok(response);
    }

    [HttpPost("tickets/claim")]
    public async Task<IActionResult> ClaimTicketFromQr(
        [FromBody] ScanTicketQrRequest request,
        CancellationToken cancellationToken)
    {
        Guid? userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<TicketQrScanResponse> result =
            await _ticketQrService.ScanTicketQrAsync(userId.Value, request.QrToken, cancellationToken);

        return ToActionResult(result, Ok);
    }

    [HttpGet("tickets")]
    public async Task<IActionResult> GetMyTickets(
        [FromQuery] int page = DefaultPage,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] string? status = null,
        [FromQuery] Guid? negocioId = null,
        CancellationToken cancellationToken = default)
    {
        Guid? userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        page = Math.Max(page, DefaultPage);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        DateTime now = DateTime.UtcNow;
        IQueryable<Ticket> query = _fidelityDbContext.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.UserId == userId.Value);

        if (negocioId.HasValue)
        {
            query = query.Where(ticket => ticket.NegocioId == negocioId.Value);
        }

        query = ApplyStatusFilter(query, status, now);

        int totalItems = await query.CountAsync(cancellationToken);
        List<Ticket> tickets = await query
            .OrderByDescending(ticket => ticket.CreatedAtUtc)
            .ThenByDescending(ticket => ticket.UpdatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        IReadOnlyCollection<UserPortalTicketResponse> items = await MapTicketsAsync(tickets, now, cancellationToken);

        return Ok(new PaginatedResponse<UserPortalTicketResponse>
        {
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize),
            Items = items
        });
    }

    [HttpGet("tickets/{ticketId:guid}")]
    public async Task<IActionResult> GetMyTicket(Guid ticketId, CancellationToken cancellationToken)
    {
        Guid? userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        DateTime now = DateTime.UtcNow;
        Ticket? ticket = await _fidelityDbContext.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(ticket => ticket.Id == ticketId && ticket.UserId == userId.Value, cancellationToken);

        if (ticket is null)
        {
            return NotFound(new { message = "Ticket no encontrado." });
        }

        UserPortalTicketResponse response = (await MapTicketsAsync([ticket], now, cancellationToken)).Single();
        return Ok(response);
    }

    [HttpGet("tickets/{ticketId:guid}/qr")]
    public async Task<IActionResult> GetMyTicketQr(Guid ticketId, CancellationToken cancellationToken)
    {
        Guid? userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        ServiceResult<AssignedTicketQrResponse> result =
            await _ticketQrService.GenerateAssignedTicketQrAsync(ticketId, userId.Value, cancellationToken);

        return ToActionResult(result, Ok);
    }

    private async Task<IReadOnlyCollection<UserPortalTicketResponse>> MapTicketsAsync(
        IReadOnlyCollection<Ticket> tickets,
        DateTime now,
        CancellationToken cancellationToken)
    {
        Guid[] negocioIds = tickets.Select(ticket => ticket.NegocioId).Distinct().ToArray();
        HashSet<Guid> negocioIdSet = negocioIds.ToHashSet();
        Dictionary<Guid, Negocio> negocios = (await _negociosDbContext.Negocios
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .Where(negocio => negocioIdSet.Contains(negocio.Id))
            .ToDictionary(negocio => negocio.Id);

        return tickets.Select(ticket =>
        {
            negocios.TryGetValue(ticket.NegocioId, out Negocio? negocio);

            return new UserPortalTicketResponse
            {
                Id = ticket.Id,
                Negocio = new UserPortalBusinessSummaryResponse
                {
                    Id = ticket.NegocioId,
                    Nombre = negocio?.NombreComercial ?? string.Empty,
                    Slug = negocio?.SlugPortal ?? string.Empty,
                    LogoUrl = negocio?.LogoPrincipalUrl,
                    IconoUrl = negocio?.IconoUrl
                },
                Titulo = ticket.TituloCanje ?? ticket.Nombre,
                Descripcion = ticket.Descripcion,
                Recompensa = ticket.BeneficioEspecialResumen ?? ticket.MensajeMarketing ?? ticket.Descripcion,
                Tipo = ticket.Tipo,
                Categoria = ticket.CategoriaEnvioEspecial,
                Estado = ResolveTicketStatus(ticket, now),
                ProgresoActual = ticket.UsosConsumidos,
                ProgresoObjetivo = ticket.MaxUsosPorCliente ?? (ticket.EsDeUnSoloUso ? 1 : null),
                Valor = ticket.Valor,
                PuntosCoste = ticket.PuntosCoste,
                Code = ticket.CodigoVisible ?? ticket.CodigoInterno,
                FechaAltaUtc = ticket.CreatedAtUtc,
                AvailableFromUtc = ticket.AvailableFromUtc,
                FechaCaducidadUtc = ticket.ExpiresAtUtc,
                FechaCanjeUtc = ticket.UsedAtUtc,
                CondicionesUso = ticket.CondicionesUso,
                InstruccionesCanje = ticket.InstruccionesCanje,
                SourcePromotionCampaignId = ticket.SourcePromotionCampaignId,
                SourcePromotionRecipientId = ticket.SourcePromotionRecipientId,
                RecibidoPorCampana = ticket.SourcePromotionRecipientId.HasValue,
                RequiereValidacionManual = ticket.RequiereValidacionManual,
                EsDeUnSoloUso = ticket.EsDeUnSoloUso,
                Activo = ticket.Activo,
                Usado = ticket.Usado
            };
        }).ToArray();
    }

    private static IQueryable<Ticket> ApplyStatusFilter(IQueryable<Ticket> query, string? status, DateTime now)
    {
        string? normalizedStatus = status?.Trim().ToLowerInvariant();
        return normalizedStatus switch
        {
            "active" => query.Where(ticket => ticket.Activo && !ticket.Usado && ticket.ExpiresAtUtc > now),
            "redeemed" => query.Where(ticket => ticket.Usado),
            "expired" => query.Where(ticket => !ticket.Usado && ticket.ExpiresAtUtc <= now),
            "inactive" => query.Where(ticket => !ticket.Activo),
            _ => query
        };
    }

    private static string ResolveTicketStatus(Ticket ticket, DateTime now)
    {
        if (ticket.Usado)
        {
            return "redeemed";
        }

        if (!ticket.Activo)
        {
            return "inactive";
        }

        if (ticket.ExpiresAtUtc <= now)
        {
            return "expired";
        }

        if (ticket.AvailableFromUtc.HasValue && ticket.AvailableFromUtc.Value > now)
        {
            return "pending";
        }

        return "active";
    }

    private IActionResult ToActionResult<T>(ServiceResult<T> result, Func<T, IActionResult> onSuccess)
    {
        if (result.Succeeded && result.Data is not null)
        {
            return onSuccess(result.Data);
        }

        return result.ErrorCode switch
        {
            "validation_error" => BadRequest(new { message = result.ErrorMessage }),
            "not_found" => NotFound(new { message = result.ErrorMessage }),
            "conflict" => Conflict(new { message = result.ErrorMessage }),
            "forbidden" => StatusCode(StatusCodes.Status403Forbidden, new { message = result.ErrorMessage }),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new { message = result.ErrorMessage ?? "Error interno del servidor." })
        };
    }

    private Guid? GetCurrentUserId()
    {
        string? value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out Guid userId) ? userId : null;
    }

    private static DateTime? MaxDate(params DateTime?[] values)
        => values.Where(value => value.HasValue).Select(value => value!.Value).DefaultIfEmpty().Max();
}
