using System.Security.Claims;
using System.Data.Common;
using Dynamic.Fidelity.Application.DTOs.Responses;
using Dynamic.Fidelity.Domain.Enums;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Dynamic.Negocios.Domain.Entities;
using Dynamic.Negocios.Domain.Enums;
using Dynamic.Negocios.Infrastructure.Persistence;
using Dynamic.Users.Domain.Entities;
using Dynamic.Users.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DynamicApi.Controllers;

[ApiController]
[Authorize(Policy = "BusinessStaffAuth")]
[Route("api/backoffice/negocios/{negocioId:guid}/audience")]
public class BusinessAudienceController : ControllerBase
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    private readonly DynamicNegociosDbContext _negociosDbContext;
    private readonly DynamicFidelityDbContext _fidelityDbContext;
    private readonly DynamicUsersDbContext _usersDbContext;

    public BusinessAudienceController(
        DynamicNegociosDbContext negociosDbContext,
        DynamicFidelityDbContext fidelityDbContext,
        DynamicUsersDbContext usersDbContext)
    {
        _negociosDbContext = negociosDbContext;
        _fidelityDbContext = fidelityDbContext;
        _usersDbContext = usersDbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAudience(
        Guid negocioId,
        [FromQuery] BusinessAudienceQueryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            IActionResult? authorization = await AuthorizeAudienceAsync(negocioId, cancellationToken);
            if (authorization is not null)
            {
                return authorization;
            }

            int page = Math.Max(request.Page.GetValueOrDefault(DefaultPage), DefaultPage);
            int pageSize = Math.Clamp(request.PageSize.GetValueOrDefault(DefaultPageSize), 1, MaxPageSize);

            List<AudienceRowProjection> audienceRows = await GetAudienceRowsAsync(negocioId, request, cancellationToken);
            List<Guid> userIds = audienceRows.Select(audience => audience.UserId).Distinct().ToList();

            Dictionary<Guid, UserAccount> users = userIds.Count == 0
                ? []
                : await _usersDbContext.Users
                    .AsNoTracking()
                    .Where(user => userIds.Contains(user.Id))
                    .ToDictionaryAsync(user => user.Id, cancellationToken);

            Dictionary<Guid, AudiencePointsStats> pointsStats = await GetPointsStatsAsync(negocioId, userIds, cancellationToken);
            Dictionary<Guid, AudienceTicketStats> ticketStats = await GetTicketStatsAsync(negocioId, userIds, cancellationToken);
            Dictionary<Guid, AudienceRevenueStats> revenueStats = await GetRevenueStatsAsync(negocioId, userIds, cancellationToken);

            IEnumerable<BusinessAudienceMemberResponse> members = audienceRows.Select(audience =>
            {
                users.TryGetValue(audience.UserId, out UserAccount? user);
                pointsStats.TryGetValue(audience.UserId, out AudiencePointsStats? points);
                ticketStats.TryGetValue(audience.UserId, out AudienceTicketStats? tickets);
                revenueStats.TryGetValue(audience.UserId, out AudienceRevenueStats? revenue);

                return new BusinessAudienceMemberResponse
                {
                    AudienceId = audience.Id,
                    NegocioId = audience.NegocioId,
                    UserId = audience.UserId,
                    DisplayName = ResolveUserDisplayName(user),
                    Email = user?.Email,
                    PhoneNumber = user?.PhoneNumber,
                    AvatarUrl = user?.AvatarUrl,
                    PostalCode = user?.PostalCode,
                    Province = user?.Province?.ToString(),
                    Gender = user?.Gender.ToString(),
                    AgeAtRegistration = user?.AgeAtRegistration,
                    MarketingAccepted = user?.MarketingAccepted ?? false,
                    RegistrationCompleted = user?.RegistrationCompleted ?? false,
                    UserStatus = user?.Status.ToString(),
                    Active = audience.Activa && audience.FechaBajaUtc == null,
                    Favorite = audience.EsFavorito,
                    Origin = audience.OrigenAlta,
                    LastActivityOrigin = audience.UltimaActividadOrigen,
                    JoinedAtUtc = audience.FechaAltaUtc,
                    LeftAtUtc = audience.FechaBajaUtc,
                    LastActivityAtUtc = MaxDate(audience.UltimaActividadUtc, points?.LastPointsActivityUtc, tickets?.LastTicketActivityUtc),
                    CurrentPoints = points?.CurrentPoints ?? 0,
                    TotalPointsEarned = points?.TotalPointsEarned ?? 0,
                    TotalPointsSpent = points?.TotalPointsSpent ?? 0,
                    TicketsTotal = tickets?.TicketsTotal ?? 0,
                    TicketsActive = tickets?.TicketsActive ?? 0,
                    TicketsRedeemed = tickets?.TicketsRedeemed ?? 0,
                    TicketsExpired = tickets?.TicketsExpired ?? 0,
                    LastTicketActivityAtUtc = tickets?.LastTicketActivityUtc,
                    TrackedRevenue = revenue?.TrackedRevenue ?? 0,
                    TicketPurchaseAmount = revenue?.TicketPurchaseAmount ?? 0,
                    LastPurchaseAtUtc = MaxDate(revenue?.LastPointsPurchaseAtUtc, revenue?.LastTicketPurchaseAtUtc)
                };
            });

            members = ApplyInMemoryFilters(members, request);
            int totalItems = members.Count();

            BusinessAudienceSummaryResponse summary = BuildSummary(members);

            BusinessAudienceMemberResponse[] items = ApplySort(members, request.SortBy, request.SortDirection)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToArray();

            return Ok(new BusinessAudiencePageResponse
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize),
                Items = items,
                Summary = summary
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new BusinessAudienceErrorResponse(
                exception.GetType().Name,
                exception.Message,
                exception.InnerException?.GetType().Name,
                exception.InnerException?.Message,
                HttpContext.TraceIdentifier));
        }
    }

    [HttpGet("origins")]
    public async Task<IActionResult> GetAudienceOrigins(Guid negocioId, CancellationToken cancellationToken)
    {
        IActionResult? authorization = await AuthorizeAudienceAsync(negocioId, cancellationToken);
        if (authorization is not null)
        {
            return authorization;
        }

        string[] origins = (await GetAudienceRowsAsync(
                negocioId,
                new BusinessAudienceQueryRequest { Active = null },
                cancellationToken))
            .Select(audience => audience.OrigenAlta)
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(origin => origin)
            .Select(origin => origin!)
            .ToArray();

        return Ok(origins);
    }

    private async Task<List<AudienceRowProjection>> GetAudienceRowsAsync(
        Guid negocioId,
        BusinessAudienceQueryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            IQueryable<NegocioAudiencia> query = _negociosDbContext.NegociosAudiencias
                .AsNoTracking()
                .Where(audience => audience.NegocioId == negocioId);

            if (request.Active.HasValue)
            {
                bool active = request.Active.Value;
                query = active
                    ? query.Where(audience => audience.Activa && audience.FechaBajaUtc == null)
                    : query.Where(audience => !audience.Activa || audience.FechaBajaUtc != null);
            }

            if (request.Favorite.HasValue)
            {
                bool favorite = request.Favorite.Value;
                query = query.Where(audience => audience.EsFavorito == favorite);
            }

            if (!string.IsNullOrWhiteSpace(request.Origin))
            {
                string origin = request.Origin.Trim();
                query = query.Where(audience => audience.OrigenAlta == origin || audience.UltimaActividadOrigen == origin);
            }

            if (request.JoinedFromUtc.HasValue)
            {
                DateTime joinedFrom = NormalizeUtc(request.JoinedFromUtc.Value);
                query = query.Where(audience => audience.FechaAltaUtc >= joinedFrom);
            }

            if (request.JoinedToUtc.HasValue)
            {
                DateTime joinedTo = NormalizeUtc(request.JoinedToUtc.Value);
                query = query.Where(audience => audience.FechaAltaUtc < joinedTo);
            }

            if (request.ActivityFromUtc.HasValue)
            {
                DateTime activityFrom = NormalizeUtc(request.ActivityFromUtc.Value);
                query = query.Where(audience => audience.UltimaActividadUtc >= activityFrom);
            }

            if (request.ActivityToUtc.HasValue)
            {
                DateTime activityTo = NormalizeUtc(request.ActivityToUtc.Value);
                query = query.Where(audience => audience.UltimaActividadUtc < activityTo);
            }

            return await query
                .Select(audience => new AudienceRowProjection(
                    audience.Id,
                    audience.NegocioId,
                    audience.UserId,
                    audience.Activa,
                    audience.EsFavorito,
                    audience.OrigenAlta,
                    audience.UltimaActividadOrigen,
                    audience.FechaAltaUtc,
                    audience.FechaBajaUtc,
                    audience.UltimaActividadUtc))
                .ToListAsync(cancellationToken);
        }
        catch (DbException)
        {
            IQueryable<NegocioUsuarioVinculacion> legacyQuery = _negociosDbContext.NegociosUsuariosVinculaciones
                .AsNoTracking()
                .Where(link =>
                    link.NegocioId == negocioId &&
                    link.TipoVinculacion == TipoVinculacionNegocioUsuario.Cliente);

            if (request.Active.HasValue)
            {
                bool active = request.Active.Value;
                legacyQuery = active
                    ? legacyQuery.Where(link => link.Activa && link.RevokedAtUtc == null)
                    : legacyQuery.Where(link => !link.Activa || link.RevokedAtUtc != null);
            }

            if (request.Favorite == true)
            {
                return [];
            }

            if (!string.IsNullOrWhiteSpace(request.Origin))
            {
                string origin = request.Origin.Trim();
                legacyQuery = legacyQuery.Where(link => link.OrigenVinculacion == origin);
            }

            if (request.JoinedFromUtc.HasValue)
            {
                DateTime joinedFrom = NormalizeUtc(request.JoinedFromUtc.Value);
                legacyQuery = legacyQuery.Where(link => link.CreatedAtUtc >= joinedFrom);
            }

            if (request.JoinedToUtc.HasValue)
            {
                DateTime joinedTo = NormalizeUtc(request.JoinedToUtc.Value);
                legacyQuery = legacyQuery.Where(link => link.CreatedAtUtc < joinedTo);
            }

            if (request.ActivityFromUtc.HasValue)
            {
                DateTime activityFrom = NormalizeUtc(request.ActivityFromUtc.Value);
                legacyQuery = legacyQuery.Where(link => link.UpdatedAtUtc >= activityFrom);
            }

            if (request.ActivityToUtc.HasValue)
            {
                DateTime activityTo = NormalizeUtc(request.ActivityToUtc.Value);
                legacyQuery = legacyQuery.Where(link => link.UpdatedAtUtc < activityTo);
            }

            return await legacyQuery
                .Select(link => new AudienceRowProjection(
                    link.Id,
                    link.NegocioId,
                    link.UserId,
                    link.Activa,
                    false,
                    link.OrigenVinculacion,
                    link.OrigenVinculacion,
                    link.FechaAceptacionUtc ?? link.FechaInicioUtc ?? link.CreatedAtUtc,
                    link.RevokedAtUtc ?? link.FechaFinUtc,
                    link.UpdatedAtUtc))
                .ToListAsync(cancellationToken);
        }
    }

    private async Task<Dictionary<Guid, AudiencePointsStats>> GetPointsStatsAsync(
        Guid negocioId,
        List<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        var pointsRows = await _fidelityDbContext.Points
            .AsNoTracking()
            .Where(points => points.NegocioId == negocioId && userIds.Contains(points.UserId))
            .Select(points => new
            {
                points.UserId,
                points.CurrentBalance,
                points.TotalEarned,
                points.TotalSpent,
                LastPointsActivityUtc = points.LastMovementAtUtc ?? points.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return pointsRows.ToDictionary(
            points => points.UserId,
            points => new AudiencePointsStats(
                points.CurrentBalance,
                points.TotalEarned,
                points.TotalSpent,
                points.LastPointsActivityUtc));
    }

    private async Task<Dictionary<Guid, AudienceTicketStats>> GetTicketStatsAsync(
        Guid negocioId,
        List<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        DateTime now = DateTime.UtcNow;
        var ticketRows = await _fidelityDbContext.Tickets
            .AsNoTracking()
            .Where(ticket =>
                ticket.NegocioId == negocioId &&
                ticket.UserId.HasValue &&
                userIds.Contains(ticket.UserId ?? Guid.Empty) &&
                !ticket.EsPlantilla)
            .Select(ticket => new
            {
                UserId = ticket.UserId ?? Guid.Empty,
                ticket.Activo,
                ticket.Usado,
                ticket.ExpiresAtUtc,
                ticket.CreatedAtUtc,
                ticket.UpdatedAtUtc,
                ticket.UsedAtUtc
            })
            .ToListAsync(cancellationToken);

        return ticketRows
            .GroupBy(ticket => ticket.UserId)
            .ToDictionary(
                group => group.Key,
                group => new AudienceTicketStats(
                    group.Count(),
                    group.Count(ticket => ticket.Activo && !ticket.Usado && ticket.ExpiresAtUtc > now),
                    group.Count(ticket => ticket.Usado),
                    group.Count(ticket => !ticket.Usado && ticket.ExpiresAtUtc <= now),
                    group.Max(ticket => MaxDate(ticket.CreatedAtUtc, ticket.UpdatedAtUtc, ticket.UsedAtUtc))));
    }

    private async Task<Dictionary<Guid, AudienceRevenueStats>> GetRevenueStatsAsync(
        Guid negocioId,
        List<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        var transactionRows = await _fidelityDbContext.PointsTransactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.NegocioId == negocioId &&
                userIds.Contains(transaction.UserId) &&
                (transaction.TransactionType == PointsTransactionType.Earn ||
                 transaction.TransactionType == PointsTransactionType.BackofficeEarn))
            .Select(transaction => new
            {
                transaction.UserId,
                AmountEuros = transaction.AmountEuros ?? 0,
                transaction.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        List<TicketRedemptionProjection> redemptionRows;
        try
        {
            redemptionRows = await _fidelityDbContext.TicketRedemptions
                .AsNoTracking()
                .Where(redemption => redemption.NegocioId == negocioId && userIds.Contains(redemption.UserId))
                .Select(redemption => new TicketRedemptionProjection(
                    redemption.UserId,
                    redemption.PurchaseAmount ?? 0,
                    redemption.CreatedAtUtc))
                .ToListAsync(cancellationToken);
        }
        catch (DbException)
        {
            redemptionRows = [];
        }

        Dictionary<Guid, AudienceRevenueStats> result = [];

        foreach (var group in transactionRows.GroupBy(transaction => transaction.UserId))
        {
            result[group.Key] = new AudienceRevenueStats(
                group.Sum(transaction => transaction.AmountEuros),
                0,
                group.Max(transaction => transaction.CreatedAtUtc),
                null);
        }

        foreach (var group in redemptionRows.GroupBy(redemption => redemption.UserId))
        {
            result.TryGetValue(group.Key, out AudienceRevenueStats? existing);
            result[group.Key] = new AudienceRevenueStats(
                existing?.TrackedRevenue ?? 0,
                group.Sum(redemption => redemption.PurchaseAmount),
                existing?.LastPointsPurchaseAtUtc,
                group.Max(redemption => redemption.CreatedAtUtc));
        }

        return result;
    }

    private async Task<IActionResult?> AuthorizeAudienceAsync(Guid negocioId, CancellationToken cancellationToken)
    {
        bool negocioExists = await _negociosDbContext.Negocios
            .AsNoTracking()
            .AnyAsync(negocio => negocio.Id == negocioId && !negocio.IsDeleted, cancellationToken);
        if (!negocioExists)
        {
            return NotFound(new { message = "El negocio no existe." });
        }

        if (User.IsInRole("Admin"))
        {
            return null;
        }

        Guid? requesterUserId = GetClaimGuid(ClaimTypes.NameIdentifier, "sub");
        if (!requesterUserId.HasValue)
        {
            return Unauthorized();
        }

        Guid requesterId = requesterUserId.Value;
        BusinessAudienceAuthorizationProjection? link = await _negociosDbContext.NegociosUsuariosVinculaciones
            .AsNoTracking()
            .Where(item => item.NegocioId == negocioId && item.UserId == requesterId)
            .Select(item => new BusinessAudienceAuthorizationProjection(
                item.TipoVinculacion,
                item.Activa,
                item.PuedeGestionarNegocio,
                item.PuedeGestionarClientes,
                item.PuedeVerReportes,
                item.FechaInicioUtc,
                item.FechaFinUtc,
                item.RevokedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

        if (!IsActiveLink(link))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "El usuario no esta vinculado al negocio." });
        }

        bool canViewAudience =
            link!.TipoVinculacion is TipoVinculacionNegocioUsuario.Propietario or TipoVinculacionNegocioUsuario.Gerente ||
            link.PuedeGestionarNegocio ||
            link.PuedeGestionarClientes ||
            link.PuedeVerReportes;

        return canViewAudience
            ? null
            : StatusCode(StatusCodes.Status403Forbidden, new { message = "El usuario no tiene permisos para ver la audiencia." });
    }

    private static IEnumerable<BusinessAudienceMemberResponse> ApplyInMemoryFilters(
        IEnumerable<BusinessAudienceMemberResponse> members,
        BusinessAudienceQueryRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            string search = request.Search.Trim();
            members = members.Where(member =>
                Contains(member.DisplayName, search) ||
                Contains(member.Email, search) ||
                Contains(member.PhoneNumber, search) ||
                Contains(member.PostalCode, search));
        }

        if (request.MarketingAccepted.HasValue)
        {
            members = members.Where(member => member.MarketingAccepted == request.MarketingAccepted.Value);
        }

        if (request.MinPoints.HasValue)
        {
            members = members.Where(member => member.CurrentPoints >= request.MinPoints.Value);
        }

        if (request.MaxPoints.HasValue)
        {
            members = members.Where(member => member.CurrentPoints <= request.MaxPoints.Value);
        }

        if (request.HasActiveTickets.HasValue)
        {
            members = members.Where(member => request.HasActiveTickets.Value
                ? member.TicketsActive > 0
                : member.TicketsActive == 0);
        }

        if (request.HasRedeemedTickets.HasValue)
        {
            members = members.Where(member => request.HasRedeemedTickets.Value
                ? member.TicketsRedeemed > 0
                : member.TicketsRedeemed == 0);
        }

        if (request.MinTrackedRevenue.HasValue)
        {
            members = members.Where(member =>
                member.TrackedRevenue + member.TicketPurchaseAmount >= request.MinTrackedRevenue.Value);
        }

        return members;
    }

    private static IEnumerable<BusinessAudienceMemberResponse> ApplySort(
        IEnumerable<BusinessAudienceMemberResponse> members,
        string? sortBy,
        string? sortDirection)
    {
        bool descending = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        string normalizedSort = string.IsNullOrWhiteSpace(sortBy) ? "lastActivity" : sortBy.Trim();

        return normalizedSort.ToLowerInvariant() switch
        {
            "name" => descending
                ? members.OrderByDescending(member => member.DisplayName)
                : members.OrderBy(member => member.DisplayName),
            "joinedat" => descending
                ? members.OrderByDescending(member => member.JoinedAtUtc)
                : members.OrderBy(member => member.JoinedAtUtc),
            "points" => descending
                ? members.OrderByDescending(member => member.CurrentPoints)
                : members.OrderBy(member => member.CurrentPoints),
            "tickets" => descending
                ? members.OrderByDescending(member => member.TicketsTotal)
                : members.OrderBy(member => member.TicketsTotal),
            "revenue" => descending
                ? members.OrderByDescending(member => member.TrackedRevenue + member.TicketPurchaseAmount)
                : members.OrderBy(member => member.TrackedRevenue + member.TicketPurchaseAmount),
            _ => descending
                ? members.OrderByDescending(member => member.LastActivityAtUtc)
                : members.OrderBy(member => member.LastActivityAtUtc)
        };
    }

    private static BusinessAudienceSummaryResponse BuildSummary(IEnumerable<BusinessAudienceMemberResponse> members)
    {
        BusinessAudienceMemberResponse[] items = members.ToArray();
        return new BusinessAudienceSummaryResponse
        {
            TotalAudience = items.Length,
            ActiveAudience = items.Count(member => member.Active),
            FavoriteAudience = items.Count(member => member.Favorite),
            MarketingAccepted = items.Count(member => member.MarketingAccepted),
            WithPoints = items.Count(member => member.CurrentPoints > 0),
            WithActiveTickets = items.Count(member => member.TicketsActive > 0),
            WithRedeemedTickets = items.Count(member => member.TicketsRedeemed > 0),
            TotalCurrentPoints = items.Sum(member => member.CurrentPoints),
            TotalTrackedRevenue = items.Sum(member => member.TrackedRevenue),
            TotalTicketPurchaseAmount = items.Sum(member => member.TicketPurchaseAmount)
        };
    }

    private static bool IsActiveLink(BusinessAudienceAuthorizationProjection? link)
    {
        if (link is null || !link.Activa || link.RevokedAtUtc.HasValue)
        {
            return false;
        }

        DateTime now = DateTime.UtcNow;
        return (!link.FechaInicioUtc.HasValue || link.FechaInicioUtc.Value <= now) &&
               (!link.FechaFinUtc.HasValue || link.FechaFinUtc.Value >= now);
    }

    private Guid? GetClaimGuid(params string[] claimTypes)
    {
        foreach (string claimType in claimTypes)
        {
            string? value = User.FindFirstValue(claimType);
            if (Guid.TryParse(value, out Guid parsedValue))
            {
                return parsedValue;
            }
        }

        return null;
    }

    private static string ResolveUserDisplayName(UserAccount? user)
    {
        if (user is null)
        {
            return "Cliente";
        }

        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            return user.DisplayName;
        }

        string fullName = $"{user.FirstName} {user.LastName}".Trim();
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName;
        }

        return user.Email ?? user.PhoneNumber ?? user.UserName;
    }

    private static DateTime NormalizeUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    private static DateTime MaxDate(params DateTime?[] values)
        => values.Where(value => value.HasValue).Select(value => value!.Value).DefaultIfEmpty().Max();

    private static bool Contains(string? value, string search)
        => !string.IsNullOrWhiteSpace(value) &&
           value.Contains(search, StringComparison.OrdinalIgnoreCase);

    private sealed record AudiencePointsStats(
        int CurrentPoints,
        int TotalPointsEarned,
        int TotalPointsSpent,
        DateTime LastPointsActivityUtc);

    private sealed record AudienceTicketStats(
        int TicketsTotal,
        int TicketsActive,
        int TicketsRedeemed,
        int TicketsExpired,
        DateTime LastTicketActivityUtc);

    private sealed record AudienceRevenueStats(
        decimal TrackedRevenue,
        decimal TicketPurchaseAmount,
        DateTime? LastPointsPurchaseAtUtc,
        DateTime? LastTicketPurchaseAtUtc);

    private sealed record TicketRedemptionProjection(
        Guid UserId,
        decimal PurchaseAmount,
        DateTime CreatedAtUtc);

    private sealed record AudienceRowProjection(
        Guid Id,
        Guid NegocioId,
        Guid UserId,
        bool Activa,
        bool EsFavorito,
        string? OrigenAlta,
        string? UltimaActividadOrigen,
        DateTime FechaAltaUtc,
        DateTime? FechaBajaUtc,
        DateTime UltimaActividadUtc);

    private sealed record BusinessAudienceAuthorizationProjection(
        TipoVinculacionNegocioUsuario TipoVinculacion,
        bool Activa,
        bool PuedeGestionarNegocio,
        bool PuedeGestionarClientes,
        bool PuedeVerReportes,
        DateTime? FechaInicioUtc,
        DateTime? FechaFinUtc,
        DateTime? RevokedAtUtc);
}

public class BusinessAudienceQueryRequest
{
    public int? Page { get; set; }
    public int? PageSize { get; set; }
    public string? Search { get; set; }
    public bool? Active { get; set; } = true;
    public bool? Favorite { get; set; }
    public string? Origin { get; set; }
    public bool? MarketingAccepted { get; set; }
    public DateTime? JoinedFromUtc { get; set; }
    public DateTime? JoinedToUtc { get; set; }
    public DateTime? ActivityFromUtc { get; set; }
    public DateTime? ActivityToUtc { get; set; }
    public int? MinPoints { get; set; }
    public int? MaxPoints { get; set; }
    public bool? HasActiveTickets { get; set; }
    public bool? HasRedeemedTickets { get; set; }
    public decimal? MinTrackedRevenue { get; set; }
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }
}

public class BusinessAudiencePageResponse : PaginatedResponse<BusinessAudienceMemberResponse>
{
    public BusinessAudienceSummaryResponse Summary { get; set; } = new();
}

public class BusinessAudienceMemberResponse
{
    public Guid AudienceId { get; set; }
    public Guid NegocioId { get; set; }
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public string? PostalCode { get; set; }
    public string? Province { get; set; }
    public string? Gender { get; set; }
    public int? AgeAtRegistration { get; set; }
    public bool MarketingAccepted { get; set; }
    public bool RegistrationCompleted { get; set; }
    public string? UserStatus { get; set; }
    public bool Active { get; set; }
    public bool Favorite { get; set; }
    public string? Origin { get; set; }
    public string? LastActivityOrigin { get; set; }
    public DateTime JoinedAtUtc { get; set; }
    public DateTime? LeftAtUtc { get; set; }
    public DateTime LastActivityAtUtc { get; set; }
    public int CurrentPoints { get; set; }
    public int TotalPointsEarned { get; set; }
    public int TotalPointsSpent { get; set; }
    public int TicketsTotal { get; set; }
    public int TicketsActive { get; set; }
    public int TicketsRedeemed { get; set; }
    public int TicketsExpired { get; set; }
    public DateTime? LastTicketActivityAtUtc { get; set; }
    public decimal TrackedRevenue { get; set; }
    public decimal TicketPurchaseAmount { get; set; }
    public DateTime? LastPurchaseAtUtc { get; set; }
}

public class BusinessAudienceSummaryResponse
{
    public int TotalAudience { get; set; }
    public int ActiveAudience { get; set; }
    public int FavoriteAudience { get; set; }
    public int MarketingAccepted { get; set; }
    public int WithPoints { get; set; }
    public int WithActiveTickets { get; set; }
    public int WithRedeemedTickets { get; set; }
    public int TotalCurrentPoints { get; set; }
    public decimal TotalTrackedRevenue { get; set; }
    public decimal TotalTicketPurchaseAmount { get; set; }
}

public sealed record BusinessAudienceErrorResponse(
    string ExceptionType,
    string Message,
    string? InnerExceptionType = null,
    string? InnerMessage = null,
    string? TraceId = null);
