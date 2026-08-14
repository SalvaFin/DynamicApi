using Dynamic.Fidelity.Application.Common;
using Dynamic.Fidelity.Application.Contracts.Services;
using Dynamic.Fidelity.Application.DTOs.Responses;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Dynamic.Negocios.Domain.Entities;
using Dynamic.Negocios.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dynamic.Fidelity.Application.Services;

public class NegocioAudienciaService : INegocioAudienciaService
{
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 100;

    private readonly DynamicNegociosDbContext _negociosDbContext;
    private readonly DynamicFidelityDbContext _fidelityDbContext;
    private readonly IRegistrationRewardService _registrationRewardService;

    public NegocioAudienciaService(
        DynamicNegociosDbContext negociosDbContext,
        DynamicFidelityDbContext fidelityDbContext,
        IRegistrationRewardService registrationRewardService)
    {
        _negociosDbContext = negociosDbContext;
        _fidelityDbContext = fidelityDbContext;
        _registrationRewardService = registrationRewardService;
    }

    public async Task<ServiceResult<FormarParteNegocioResponse>> FormarParteAsync(
        Guid negocioId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (negocioId == Guid.Empty || userId == Guid.Empty)
        {
            return ServiceResult<FormarParteNegocioResponse>.Failure(
                "validation_error",
                "Negocio y usuario son obligatorios.");
        }

        Negocio? negocio = await _negociosDbContext.Negocios
            .FirstOrDefaultAsync(item => item.Id == negocioId && !item.IsDeleted, cancellationToken);
        if (negocio is null || !negocio.Activo || !negocio.PublicadoPortal)
        {
            return ServiceResult<FormarParteNegocioResponse>.Failure("not_found", "Negocio no encontrado.");
        }

        NegocioAudiencia? existing = await _negociosDbContext.NegociosAudiencias
            .FirstOrDefaultAsync(item => item.NegocioId == negocioId && item.UserId == userId, cancellationToken);
        bool alreadyActive = IsAudienceActive(existing);
        bool firstAudience = existing is null;

        ServiceResult<NegocioAudiencia> ensureResult = await EnsureAudienceAsync(
            negocioId,
            userId,
            "audience_join",
            cancellationToken);
        if (!ensureResult.Succeeded || ensureResult.Data is null)
        {
            return ServiceResult<FormarParteNegocioResponse>.Failure(
                ensureResult.ErrorCode ?? "validation_error",
                ensureResult.ErrorMessage ?? "No se ha podido formar parte del negocio.");
        }

        bool welcomeTicketAssigned = firstAudience &&
            await _registrationRewardService.AssignBusinessWelcomeTicketAsync(negocioId, userId, cancellationToken);

        return ServiceResult<FormarParteNegocioResponse>.Success(new FormarParteNegocioResponse
        {
            NegocioId = negocioId,
            AudienciaId = ensureResult.Data.Id,
            YaFormabaParte = alreadyActive,
            FormadoAhora = !alreadyActive,
            EsFavorito = ensureResult.Data.EsFavorito,
            PermiteCorreosPromocionales = ensureResult.Data.PermiteCorreosPromocionales,
            BonoBienvenidaRecibido = welcomeTicketAssigned
        });
    }

    public async Task<ServiceResult> DejarDeFormarParteAsync(
        Guid negocioId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (negocioId == Guid.Empty || userId == Guid.Empty)
        {
            return ServiceResult.Failure("validation_error", "Negocio y usuario son obligatorios.");
        }

        NegocioAudiencia? audiencia = await _negociosDbContext.NegociosAudiencias
            .FirstOrDefaultAsync(item => item.NegocioId == negocioId && item.UserId == userId, cancellationToken);

        DateTime now = DateTime.UtcNow;
        if (audiencia is not null)
        {
            audiencia.Activa = false;
            audiencia.EsFavorito = false;
            audiencia.FechaBajaUtc = now;
            audiencia.UpdatedAtUtc = now;
        }

        var assignedTickets = await _fidelityDbContext.Tickets
            .Where(ticket => ticket.NegocioId == negocioId && ticket.UserId == userId && !ticket.EsPlantilla)
            .ToListAsync(cancellationToken);
        _fidelityDbContext.Tickets.RemoveRange(assignedTickets);

        var points = await _fidelityDbContext.Points
            .Where(item => item.NegocioId == negocioId && item.UserId == userId)
            .ToListAsync(cancellationToken);
        _fidelityDbContext.Points.RemoveRange(points);

        var pointTransactions = await _fidelityDbContext.PointsTransactions
            .Where(transaction => transaction.NegocioId == negocioId && transaction.UserId == userId)
            .ToListAsync(cancellationToken);
        _fidelityDbContext.PointsTransactions.RemoveRange(pointTransactions);

        var pendingAssignments = await _fidelityDbContext.PendingTicketAssignments
            .Where(assignment => assignment.NegocioId == negocioId && assignment.UserId == userId)
            .ToListAsync(cancellationToken);
        _fidelityDbContext.PendingTicketAssignments.RemoveRange(pendingAssignments);

        await _fidelityDbContext.SaveChangesAsync(cancellationToken);
        await _negociosDbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success();
    }

    public async Task<ServiceResult<AudienceFavoriteResponse>> SetFavoritoAsync(
        Guid negocioId,
        Guid userId,
        bool esFavorito,
        CancellationToken cancellationToken = default)
    {
        NegocioAudiencia? audiencia = await _negociosDbContext.NegociosAudiencias
            .FirstOrDefaultAsync(item => item.NegocioId == negocioId && item.UserId == userId, cancellationToken);
        if (!IsAudienceActive(audiencia))
        {
            return ServiceResult<AudienceFavoriteResponse>.Failure(
                "not_found",
                "El usuario no forma parte de la audiencia del negocio.");
        }

        audiencia!.EsFavorito = esFavorito;
        audiencia.UpdatedAtUtc = DateTime.UtcNow;
        await _negociosDbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<AudienceFavoriteResponse>.Success(new AudienceFavoriteResponse
        {
            NegocioId = negocioId,
            AudienciaId = audiencia.Id,
            EsFavorito = audiencia.EsFavorito
        });
    }

    public async Task<ServiceResult<BusinessEmailPreferenceResponse>> UnsubscribeFromBusinessEmailsAsync(
        Guid negocioId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (negocioId == Guid.Empty || userId == Guid.Empty)
        {
            return ServiceResult<BusinessEmailPreferenceResponse>.Failure(
                "validation_error",
                "Negocio y usuario son obligatorios.");
        }

        NegocioAudiencia? audiencia = await _negociosDbContext.NegociosAudiencias
            .FirstOrDefaultAsync(item => item.NegocioId == negocioId && item.UserId == userId, cancellationToken);
        if (!IsAudienceActive(audiencia))
        {
            return ServiceResult<BusinessEmailPreferenceResponse>.Failure(
                "not_found",
                "El usuario no forma parte de la audiencia activa del negocio.");
        }

        DateTime now = DateTime.UtcNow;
        audiencia!.PermiteCorreosPromocionales = false;
        audiencia.CorreosPromocionalesRevocadosAtUtc ??= now;
        audiencia.UpdatedAtUtc = now;
        await _negociosDbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<BusinessEmailPreferenceResponse>.Success(new BusinessEmailPreferenceResponse
        {
            NegocioId = negocioId,
            PermiteCorreosPromocionales = false,
            AceptadoAtUtc = audiencia.CorreosPromocionalesAceptadosAtUtc,
            RevocadoAtUtc = audiencia.CorreosPromocionalesRevocadosAtUtc
        });
    }

    public async Task<ServiceResult<IReadOnlyCollection<UserPortalBusinessResponse>>> GetMyBusinessesAsync(
        Guid userId,
        bool soloFavoritos,
        IReadOnlyCollection<string>? tags,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize <= 0 ? DefaultPageSize : pageSize, 1, MaxPageSize);
        string[] normalizedTags = NormalizeTags(tags);

        DateTime now = DateTime.UtcNow;
        var audienceRows = await _negociosDbContext.NegociosAudiencias
            .AsNoTracking()
            .Include(audience => audience.Negocio)
            .Where(audience =>
                audience.UserId == userId &&
                audience.Activa &&
                audience.FechaBajaUtc == null &&
                audience.Negocio != null &&
                !audience.Negocio.IsDeleted &&
                (!soloFavoritos || audience.EsFavorito))
            .ToListAsync(cancellationToken);

        if (normalizedTags.Length > 0)
        {
            audienceRows = audienceRows
                .Where(audience => BusinessHasAnyTag(audience.Negocio?.Etiquetas, normalizedTags))
                .ToList();
        }

        Guid[] negocioIds = audienceRows
            .Select(audience => audience.NegocioId)
            .Distinct()
            .ToArray();

        if (negocioIds.Length == 0)
        {
            return ServiceResult<IReadOnlyCollection<UserPortalBusinessResponse>>.Success([]);
        }

        HashSet<Guid> negocioIdSet = negocioIds.ToHashSet();

        var userTicketRows = await _fidelityDbContext.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.UserId == userId)
            .Select(ticket => new
            {
                ticket.NegocioId,
                ticket.Activo,
                ticket.Usado,
                ticket.ExpiresAtUtc,
                ticket.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        Dictionary<Guid, TicketBusinessStats> ticketStats = userTicketRows
            .Where(ticket => negocioIdSet.Contains(ticket.NegocioId))
            .GroupBy(ticket => ticket.NegocioId)
            .ToDictionary(
                group => group.Key,
                group => new TicketBusinessStats(
                    group.Key,
                    group.Count(),
                    group.Count(ticket => ticket.Activo && !ticket.Usado && ticket.ExpiresAtUtc > now),
                    group.Max(ticket => ticket.UpdatedAtUtc)));

        var userPointRows = await _fidelityDbContext.Points
            .AsNoTracking()
            .Where(points => points.UserId == userId)
            .Select(points => new
            {
                points.NegocioId,
                PuntosActuales = points.CurrentBalance,
                LastPointsActivityUtc = points.LastMovementAtUtc ?? points.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        Dictionary<Guid, PointsBusinessStats> pointStats = userPointRows
            .Where(points => negocioIdSet.Contains(points.NegocioId))
            .ToDictionary(
                points => points.NegocioId,
                points => new PointsBusinessStats(
                    points.NegocioId,
                    points.PuntosActuales,
                    points.LastPointsActivityUtc));

        IReadOnlyCollection<UserPortalBusinessResponse> response = audienceRows
            .Select(audience =>
            {
                Negocio negocio = audience.Negocio!;
                ticketStats.TryGetValue(audience.NegocioId, out TicketBusinessStats? ticket);
                pointStats.TryGetValue(audience.NegocioId, out PointsBusinessStats? points);
                string[] businessTags = SplitTags(negocio.Etiquetas);

                return new UserPortalBusinessResponse
                {
                    Id = negocio.Id,
                    Nombre = negocio.NombreComercial,
                    Slug = negocio.SlugPortal,
                    LogoUrl = negocio.LogoPrincipalUrl,
                    IconoUrl = negocio.IconoUrl,
                    ImagenCoverUrl = negocio.ImagenCoverUrl,
                    Categoria = negocio.CategoriaPrincipal,
                    Etiquetas = negocio.Etiquetas,
                    Tags = businessTags,
                    Ciudad = negocio.Ciudad,
                    Provincia = negocio.Provincia,
                    Activo = negocio.Activo,
                    PublicadoPortal = negocio.PublicadoPortal,
                    AudienciaId = audience.Id,
                    FormaParteAudiencia = true,
                    EsFavorito = audience.EsFavorito,
                    PermiteCorreosPromocionales = audience.PermiteCorreosPromocionales,
                    PuntosActuales = points?.PuntosActuales ?? 0,
                    TicketsActivos = ticket?.TicketsActivos ?? 0,
                    TicketsTotales = ticket?.TicketsTotales ?? 0,
                    LinkedFromTickets = ticket is not null,
                    LinkedFromPoints = points is not null,
                    LinkedFromVinculacion = true,
                    TipoVinculacion = "Audiencia",
                    FechaUltimaActividadUtc = MaxDate(
                        ticket?.LastTicketActivityUtc,
                        points?.LastPointsActivityUtc,
                        audience.UltimaActividadUtc)
                };
            })
            .OrderByDescending(negocio => negocio.FechaUltimaActividadUtc)
            .ThenBy(negocio => negocio.Nombre)
            .Take(pageSize)
            .ToArray();

        return ServiceResult<IReadOnlyCollection<UserPortalBusinessResponse>>.Success(response);
    }

    public async Task<ServiceResult<IReadOnlyCollection<string>>> GetMyBusinessTagsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        string?[] rawTags = await _negociosDbContext.NegociosAudiencias
            .AsNoTracking()
            .Include(audience => audience.Negocio)
            .Where(audience =>
                audience.UserId == userId &&
                audience.Activa &&
                audience.FechaBajaUtc == null &&
                audience.Negocio != null &&
                !audience.Negocio.IsDeleted)
            .Select(audience => audience.Negocio!.Etiquetas)
            .ToArrayAsync(cancellationToken);

        IReadOnlyCollection<string> tags = rawTags
            .SelectMany(SplitTags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag)
            .ToArray();

        return ServiceResult<IReadOnlyCollection<string>>.Success(tags);
    }

    public async Task<ServiceResult<NegocioAudiencia>> EnsureAudienceAsync(
        Guid negocioId,
        Guid userId,
        string origin,
        CancellationToken cancellationToken = default)
    {
        if (negocioId == Guid.Empty || userId == Guid.Empty)
        {
            return ServiceResult<NegocioAudiencia>.Failure("validation_error", "Negocio y usuario son obligatorios.");
        }

        bool existsBusiness = await _negociosDbContext.Negocios
            .AsNoTracking()
            .AnyAsync(negocio => negocio.Id == negocioId && !negocio.IsDeleted, cancellationToken);
        if (!existsBusiness)
        {
            return ServiceResult<NegocioAudiencia>.Failure("not_found", "Negocio no encontrado.");
        }

        DateTime now = DateTime.UtcNow;
        string normalizedOrigin = Normalize(origin) ?? "unknown";

        NegocioAudiencia? audience = await _negociosDbContext.NegociosAudiencias
            .FirstOrDefaultAsync(item => item.NegocioId == negocioId && item.UserId == userId, cancellationToken);

        if (audience is null)
        {
            audience = new NegocioAudiencia
            {
                Id = Guid.NewGuid(),
                NegocioId = negocioId,
                UserId = userId,
                Activa = true,
                PermiteCorreosPromocionales = true,
                CorreosPromocionalesAceptadosAtUtc = now,
                OrigenAlta = normalizedOrigin,
                UltimaActividadOrigen = normalizedOrigin,
                FechaAltaUtc = now,
                UltimaActividadUtc = now,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            await _negociosDbContext.NegociosAudiencias.AddAsync(audience, cancellationToken);
        }
        else
        {
            audience.Activa = true;
            audience.FechaBajaUtc = null;
            audience.OrigenAlta ??= normalizedOrigin;
            audience.UltimaActividadOrigen = normalizedOrigin;
            audience.UltimaActividadUtc = now;
            audience.UpdatedAtUtc = now;
        }

        await _negociosDbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<NegocioAudiencia>.Success(audience);
    }

    public async Task TouchAudienceActivityAsync(
        Guid negocioId,
        Guid userId,
        string origin,
        CancellationToken cancellationToken = default)
    {
        await EnsureAudienceAsync(negocioId, userId, origin, cancellationToken);
    }

    private static bool IsAudienceActive(NegocioAudiencia? audience)
        => audience is not null && audience.Activa && !audience.FechaBajaUtc.HasValue;

    private sealed record TicketBusinessStats(
        Guid NegocioId,
        int TicketsTotales,
        int TicketsActivos,
        DateTime LastTicketActivityUtc);

    private sealed record PointsBusinessStats(
        Guid NegocioId,
        int PuntosActuales,
        DateTime LastPointsActivityUtc);

    private static DateTime? MaxDate(params DateTime?[] values)
        => values.Where(value => value.HasValue).Select(value => value!.Value).DefaultIfEmpty().Max();

    private static string[] NormalizeTags(IReadOnlyCollection<string>? tags)
        => tags is null
            ? []
            : tags.SelectMany(SplitTags)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

    private static string[] SplitTags(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .ToArray();

    private static bool BusinessHasAnyTag(string? businessTags, IReadOnlyCollection<string> filters)
    {
        if (filters.Count == 0)
        {
            return true;
        }

        string[] tags = SplitTags(businessTags);
        return tags.Any(tag => filters.Contains(tag, StringComparer.OrdinalIgnoreCase));
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
