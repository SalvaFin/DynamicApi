using System.Data.Common;
using System.Security.Claims;
using Dynamic.Fidelity.Domain.Enums;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Dynamic.Negocios.Domain.Entities;
using Dynamic.Negocios.Domain.Enums;
using Dynamic.Negocios.Infrastructure.Persistence;
using Dynamic.Users.Domain.Enums;
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
    private static readonly TimeSpan RecentAudienceWindow = TimeSpan.FromDays(30);

    private readonly DynamicNegociosDbContext _negociosDbContext;
    private readonly DynamicFidelityDbContext _fidelityDbContext;
    private readonly DynamicUsersDbContext _usersDbContext;
    private readonly ILogger<BusinessAudienceController> _logger;

    public BusinessAudienceController(
        DynamicNegociosDbContext negociosDbContext,
        DynamicFidelityDbContext fidelityDbContext,
        DynamicUsersDbContext usersDbContext,
        ILogger<BusinessAudienceController> logger)
    {
        _negociosDbContext = negociosDbContext;
        _fidelityDbContext = fidelityDbContext;
        _usersDbContext = usersDbContext;
        _logger = logger;
    }

    /// <summary>
    /// Devuelve exclusivamente metricas agregadas de la audiencia. No expone filas,
    /// identificadores ni atributos de usuarios y no admite filtros que permitan
    /// inferir informacion de una persona concreta.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<BusinessAudienceSummaryResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAudience(
        Guid negocioId,
        [FromQuery] BusinessAudienceDemographicFilter request,
        CancellationToken cancellationToken)
    {
        try
        {
            IActionResult? filterError = ValidateDemographicFilter(request.MinimumAge, request.MaximumAge);
            if (filterError is not null)
            {
                return filterError;
            }

            IActionResult? authorization = await AuthorizeAudienceAsync(negocioId, cancellationToken);
            if (authorization is not null)
            {
                return authorization;
            }

            List<AudienceRowProjection> audienceRows = await GetAudienceRowsAsync(negocioId, cancellationToken);
            HashSet<Guid>? demographicUserIds = await GetDemographicUserIdsAsync(request, cancellationToken);
            if (demographicUserIds is not null)
            {
                audienceRows = audienceRows
                    .Where(audience => demographicUserIds.Contains(audience.UserId))
                    .ToList();
            }
            AudienceRowProjection[] activeAudience = audienceRows
                .Where(audience => audience.Active)
                .ToArray();
            List<Guid> activeUserIds = activeAudience
                .Select(audience => audience.UserId)
                .Distinct()
                .ToList();

            AudiencePointsSummary points = await GetPointsSummaryAsync(negocioId, activeUserIds, cancellationToken);
            AudienceTicketSummary tickets = await GetTicketSummaryAsync(negocioId, activeUserIds, cancellationToken);
            AudienceRevenueSummary revenue = await GetRevenueSummaryAsync(negocioId, activeUserIds, cancellationToken);

            DateTime recentThresholdUtc = DateTime.UtcNow.Subtract(RecentAudienceWindow);
            decimal totalMoneyEarned = revenue.PointsTrackedRevenue + revenue.TicketPurchaseAmount;

            return Ok(new BusinessAudienceSummaryResponse
            {
                TotalAudience = audienceRows.Count,
                ActiveAudience = activeAudience.Length,
                InactiveAudience = audienceRows.Count - activeAudience.Length,
                FavoriteAudience = activeAudience.Count(audience => audience.Favorite),
                EmailReachableAudience = activeAudience.Count(audience => audience.EmailConsent),
                NewAudienceLast30Days = activeAudience.Count(audience => audience.JoinedAtUtc >= recentThresholdUtc),
                RecentlyActiveAudience = activeAudience.Count(audience => audience.LastActivityAtUtc >= recentThresholdUtc),
                WithPoints = points.PeopleWithPoints,
                WithTickets = tickets.PeopleWithTickets,
                WithActiveTickets = tickets.PeopleWithActiveTickets,
                WithRedeemedTickets = tickets.PeopleWithRedeemedTickets,
                TotalCurrentPoints = points.CurrentBalance,
                TotalPointsEarned = points.TotalEarned,
                TotalPointsSpent = points.TotalSpent,
                PointsRedemptionRate = Percentage(points.TotalSpent, points.TotalEarned),
                TotalTicketsAssigned = tickets.TotalAssigned,
                TotalTicketsActive = tickets.TotalActive,
                TotalTicketsRedeemed = tickets.TotalRedeemed,
                TotalTicketsExpired = tickets.TotalExpired,
                TicketRedemptionRate = Percentage(tickets.TotalRedeemed, tickets.TotalAssigned),
                TotalTrackedRevenue = revenue.PointsTrackedRevenue,
                TotalTicketPurchaseAmount = revenue.TicketPurchaseAmount,
                TotalMoneyEarned = totalMoneyEarned,
                AverageMoneyEarnedPerActivePerson = activeAudience.Length == 0
                    ? 0
                    : decimal.Round(totalMoneyEarned / activeAudience.Length, 2)
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "No se pudieron calcular los KPIs de audiencia del negocio {NegocioId}.", negocioId);
            return StatusCode(StatusCodes.Status500InternalServerError, new BusinessAudienceErrorResponse(
                "No se pudieron calcular las metricas de audiencia.",
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

        string[] origins = (await GetAudienceRowsAsync(negocioId, cancellationToken))
            .Select(audience => audience.Origin)
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(origin => origin)
            .Select(origin => origin!)
            .ToArray();

        return Ok(origins);
    }

    private async Task<List<AudienceRowProjection>> GetAudienceRowsAsync(
        Guid negocioId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _negociosDbContext.NegociosAudiencias
                .AsNoTracking()
                .Where(audience => audience.NegocioId == negocioId)
                .Select(audience => new AudienceRowProjection(
                    audience.UserId,
                    audience.Activa && audience.FechaBajaUtc == null,
                    audience.EsFavorito,
                    audience.PermiteCorreosPromocionales,
                    audience.OrigenAlta,
                    audience.FechaAltaUtc,
                    audience.UltimaActividadUtc))
                .ToListAsync(cancellationToken);
        }
        catch (DbException)
        {
            return await _negociosDbContext.NegociosUsuariosVinculaciones
                .AsNoTracking()
                .Where(link =>
                    link.NegocioId == negocioId &&
                    link.TipoVinculacion == TipoVinculacionNegocioUsuario.Cliente)
                .Select(link => new AudienceRowProjection(
                    link.UserId,
                    link.Activa && link.RevokedAtUtc == null,
                    false,
                    false,
                    link.OrigenVinculacion,
                    link.FechaAceptacionUtc ?? link.FechaInicioUtc ?? link.CreatedAtUtc,
                    link.UpdatedAtUtc))
                .ToListAsync(cancellationToken);
        }
    }

    private async Task<AudiencePointsSummary> GetPointsSummaryAsync(
        Guid negocioId,
        List<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new AudiencePointsSummary();
        }

        var rows = await _fidelityDbContext.Points
            .AsNoTracking()
            .Where(points => points.NegocioId == negocioId && userIds.Contains(points.UserId))
            .Select(points => new
            {
                points.CurrentBalance,
                points.TotalEarned,
                points.TotalSpent
            })
            .ToListAsync(cancellationToken);

        return new AudiencePointsSummary(
            rows.Count(points => points.CurrentBalance > 0),
            rows.Sum(points => points.CurrentBalance),
            rows.Sum(points => points.TotalEarned),
            rows.Sum(points => points.TotalSpent));
    }

    private async Task<AudienceTicketSummary> GetTicketSummaryAsync(
        Guid negocioId,
        List<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new AudienceTicketSummary();
        }

        DateTime now = DateTime.UtcNow;
        var rows = await _fidelityDbContext.Tickets
            .AsNoTracking()
            .Where(ticket =>
                ticket.NegocioId == negocioId &&
                ticket.UserId.HasValue &&
                userIds.Contains(ticket.UserId.Value) &&
                !ticket.EsPlantilla)
            .Select(ticket => new
            {
                UserId = ticket.UserId!.Value,
                ticket.Activo,
                ticket.Usado,
                ticket.ExpiresAtUtc
            })
            .ToListAsync(cancellationToken);

        return new AudienceTicketSummary(
            rows.Select(ticket => ticket.UserId).Distinct().Count(),
            rows.Where(ticket => ticket.Activo && !ticket.Usado && ticket.ExpiresAtUtc > now)
                .Select(ticket => ticket.UserId).Distinct().Count(),
            rows.Where(ticket => ticket.Usado).Select(ticket => ticket.UserId).Distinct().Count(),
            rows.Count,
            rows.Count(ticket => ticket.Activo && !ticket.Usado && ticket.ExpiresAtUtc > now),
            rows.Count(ticket => ticket.Usado),
            rows.Count(ticket => !ticket.Usado && ticket.ExpiresAtUtc <= now));
    }

    private async Task<AudienceRevenueSummary> GetRevenueSummaryAsync(
        Guid negocioId,
        List<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new AudienceRevenueSummary();
        }

        decimal pointsTrackedRevenue = await _fidelityDbContext.PointsTransactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.NegocioId == negocioId &&
                userIds.Contains(transaction.UserId) &&
                (transaction.TransactionType == PointsTransactionType.Earn ||
                 transaction.TransactionType == PointsTransactionType.BackofficeEarn))
            .SumAsync(transaction => transaction.AmountEuros ?? 0m, cancellationToken);

        decimal ticketPurchaseAmount;
        try
        {
            ticketPurchaseAmount = await _fidelityDbContext.TicketRedemptions
                .AsNoTracking()
                .Where(redemption => redemption.NegocioId == negocioId && userIds.Contains(redemption.UserId))
                .SumAsync(redemption => redemption.PurchaseAmount ?? 0m, cancellationToken);
        }
        catch (DbException)
        {
            ticketPurchaseAmount = 0;
        }

        return new AudienceRevenueSummary(pointsTrackedRevenue, ticketPurchaseAmount);
    }

    private async Task<HashSet<Guid>?> GetDemographicUserIdsAsync(
        BusinessAudienceDemographicFilter filter,
        CancellationToken cancellationToken)
    {
        if (!filter.Gender.HasValue && !filter.MinimumAge.HasValue && !filter.MaximumAge.HasValue)
        {
            return null;
        }

        IQueryable<Dynamic.Users.Domain.Entities.UserAccount> query = _usersDbContext.Users.AsNoTracking();

        if (filter.Gender.HasValue)
        {
            UserGender gender = filter.Gender.Value;
            query = query.Where(user => user.Gender == gender);
        }

        DateTime todayUtc = DateTime.UtcNow.Date;
        if (filter.MinimumAge.HasValue)
        {
            DateTime latestBirthDate = todayUtc.AddYears(-filter.MinimumAge.Value);
            query = query.Where(user => user.BirthDate.HasValue && user.BirthDate.Value <= latestBirthDate);
        }

        if (filter.MaximumAge.HasValue)
        {
            DateTime earliestBirthDateExclusive = todayUtc.AddYears(-(filter.MaximumAge.Value + 1));
            query = query.Where(user => user.BirthDate.HasValue && user.BirthDate.Value > earliestBirthDateExclusive);
        }

        return (await query.Select(user => user.Id).ToListAsync(cancellationToken)).ToHashSet();
    }

    private IActionResult? ValidateDemographicFilter(int? minimumAge, int? maximumAge)
    {
        if (minimumAge is < 0 or > 130 || maximumAge is < 0 or > 130)
        {
            return BadRequest(new { message = "Las edades deben estar entre 0 y 130." });
        }

        return minimumAge.HasValue && maximumAge.HasValue && minimumAge > maximumAge
            ? BadRequest(new { message = "La edad minima no puede ser mayor que la edad maxima." })
            : null;
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

    private static decimal Percentage(int numerator, int denominator)
        => denominator <= 0 ? 0 : decimal.Round((decimal)numerator / denominator * 100m, 2);

    private sealed record AudienceRowProjection(
        Guid UserId,
        bool Active,
        bool Favorite,
        bool EmailConsent,
        string? Origin,
        DateTime JoinedAtUtc,
        DateTime LastActivityAtUtc);

    private sealed record AudiencePointsSummary(
        int PeopleWithPoints = 0,
        int CurrentBalance = 0,
        int TotalEarned = 0,
        int TotalSpent = 0);

    private sealed record AudienceTicketSummary(
        int PeopleWithTickets = 0,
        int PeopleWithActiveTickets = 0,
        int PeopleWithRedeemedTickets = 0,
        int TotalAssigned = 0,
        int TotalActive = 0,
        int TotalRedeemed = 0,
        int TotalExpired = 0);

    private sealed record AudienceRevenueSummary(
        decimal PointsTrackedRevenue = 0,
        decimal TicketPurchaseAmount = 0);

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

public class BusinessAudienceSummaryResponse
{
    public int TotalAudience { get; set; }
    public int ActiveAudience { get; set; }
    public int InactiveAudience { get; set; }
    public int FavoriteAudience { get; set; }
    public int EmailReachableAudience { get; set; }
    public int NewAudienceLast30Days { get; set; }
    public int RecentlyActiveAudience { get; set; }
    public int WithPoints { get; set; }
    public int WithTickets { get; set; }
    public int WithActiveTickets { get; set; }
    public int WithRedeemedTickets { get; set; }
    public int TotalCurrentPoints { get; set; }
    public int TotalPointsEarned { get; set; }
    public int TotalPointsSpent { get; set; }
    public decimal PointsRedemptionRate { get; set; }
    public int TotalTicketsAssigned { get; set; }
    public int TotalTicketsActive { get; set; }
    public int TotalTicketsRedeemed { get; set; }
    public int TotalTicketsExpired { get; set; }
    public decimal TicketRedemptionRate { get; set; }
    public decimal TotalTrackedRevenue { get; set; }
    public decimal TotalTicketPurchaseAmount { get; set; }
    public decimal TotalMoneyEarned { get; set; }
    public decimal AverageMoneyEarnedPerActivePerson { get; set; }
}

public sealed class BusinessAudienceDemographicFilter
{
    public UserGender? Gender { get; set; }
    public int? MinimumAge { get; set; }
    public int? MaximumAge { get; set; }
}

public sealed record BusinessAudienceErrorResponse(
    string Message,
    string? TraceId = null);
