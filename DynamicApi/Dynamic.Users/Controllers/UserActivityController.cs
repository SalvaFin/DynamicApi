using System.Security.Claims;
using Dynamic.Fidelity.Application.DTOs.Responses;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Domain.Enums;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Dynamic.Negocios.Domain.Entities;
using Dynamic.Negocios.Infrastructure.Persistence;
using Dynamic.Users.Application.DTOs.Responses;
using Dynamic.Users.Domain.Entities;
using Dynamic.Users.Domain.Enums;
using Dynamic.Users.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dynamic.Users.Controllers;

[ApiController]
[Authorize]
[Route("api/users/me")]
public class UserActivityController : ControllerBase
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly DynamicUsersDbContext _usersDbContext;
    private readonly DynamicFidelityDbContext _fidelityDbContext;
    private readonly DynamicNegociosDbContext _negociosDbContext;

    public UserActivityController(
        DynamicUsersDbContext usersDbContext,
        DynamicFidelityDbContext fidelityDbContext,
        DynamicNegociosDbContext negociosDbContext)
    {
        _usersDbContext = usersDbContext;
        _fidelityDbContext = fidelityDbContext;
        _negociosDbContext = negociosDbContext;
    }

    [HttpGet("activity")]
    public async Task<IActionResult> GetMyActivity(
        [FromQuery] int page = DefaultPage,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] string? category = null,
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
        string? normalizedCategory = NormalizeCategory(category);

        List<UserActivityItemResponse> activities = [];

        if (normalizedCategory == "auth")
        {
            activities.AddRange(await GetAuthActivitiesAsync(userId.Value, cancellationToken));
        }

        if (normalizedCategory is "core" or "points")
        {
            activities.AddRange(await GetPointActivitiesAsync(userId.Value, negocioId, cancellationToken));
        }

        if (normalizedCategory is "core" or "tickets")
        {
            activities.AddRange(await GetTicketActivitiesAsync(userId.Value, negocioId, cancellationToken));
        }

        List<UserActivityItemResponse> ordered = activities
            .OrderByDescending(activity => activity.CreatedAtUtc)
            .ToList();

        int totalItems = ordered.Count;

        return Ok(new PaginatedResponse<UserActivityItemResponse>
        {
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize),
            Items = ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToArray()
        });
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> GetMyTransactions(
        [FromQuery] int page = DefaultPage,
        [FromQuery] int pageSize = DefaultPageSize,
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

        IQueryable<PointsTransaction> query = _fidelityDbContext.PointsTransactions
            .AsNoTracking()
            .Where(transaction => transaction.UserId == userId.Value);

        if (negocioId.HasValue)
        {
            query = query.Where(transaction => transaction.NegocioId == negocioId.Value);
        }

        int totalItems = await query.CountAsync(cancellationToken);
        List<PointsTransaction> transactions = await query
            .OrderByDescending(transaction => transaction.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        Dictionary<Guid, UserActivityBusinessSummaryResponse> negocios =
            await GetBusinessSummariesAsync(transactions.Select(transaction => transaction.NegocioId), cancellationToken);

        return Ok(new PaginatedResponse<UserTransactionHistoryItemResponse>
        {
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize),
            Items = transactions.Select(transaction => new UserTransactionHistoryItemResponse
            {
                TransactionId = transaction.Id,
                NegocioId = transaction.NegocioId,
                Negocio = negocios.GetValueOrDefault(transaction.NegocioId),
                TransactionType = transaction.TransactionType,
                Direction = ResolveTransactionDirection(transaction.TransactionType),
                AmountEuros = transaction.AmountEuros,
                PointsAmount = transaction.PointsAmount,
                BalanceBefore = transaction.BalanceBefore,
                BalanceAfter = transaction.BalanceAfter,
                Reason = transaction.Reason,
                Reference = transaction.Reference,
                CreatedAtUtc = transaction.CreatedAtUtc
            }).ToArray()
        });
    }

    private async Task<IReadOnlyCollection<UserActivityItemResponse>> GetAuthActivitiesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        List<UserAuthEvent> events = await _usersDbContext.UserAuthEvents
            .AsNoTracking()
            .Where(authEvent => authEvent.UserId == userId)
            .OrderByDescending(authEvent => authEvent.CreatedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken);

        return events.Select(authEvent => new UserActivityItemResponse
        {
            Id = $"auth:{authEvent.Id:N}",
            Category = "auth",
            Type = authEvent.EventType.ToString(),
            Title = ResolveAuthTitle(authEvent.EventType, authEvent.Succeeded),
            Description = authEvent.Succeeded ? authEvent.ClientSummary : authEvent.FailureReason,
            Status = authEvent.Succeeded ? "success" : "failed",
            CreatedAtUtc = authEvent.CreatedAtUtc
        }).ToArray();
    }

    private async Task<IReadOnlyCollection<UserActivityItemResponse>> GetPointActivitiesAsync(
        Guid userId,
        Guid? negocioId,
        CancellationToken cancellationToken)
    {
        IQueryable<PointsTransaction> query = _fidelityDbContext.PointsTransactions
            .AsNoTracking()
            .Where(transaction => transaction.UserId == userId);

        if (negocioId.HasValue)
        {
            query = query.Where(transaction => transaction.NegocioId == negocioId.Value);
        }

        List<PointsTransaction> transactions = await query
            .OrderByDescending(transaction => transaction.CreatedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken);

        Dictionary<Guid, UserActivityBusinessSummaryResponse> negocios =
            await GetBusinessSummariesAsync(transactions.Select(transaction => transaction.NegocioId), cancellationToken);

        return transactions.Select(transaction => new UserActivityItemResponse
        {
            Id = $"points:{transaction.Id:N}",
            Category = "points",
            Type = transaction.TransactionType.ToString(),
            Title = ResolvePointsTitle(transaction.TransactionType, transaction.PointsAmount),
            Description = transaction.Reason,
            Negocio = negocios.GetValueOrDefault(transaction.NegocioId),
            TransactionId = transaction.Id,
            PointsAmount = transaction.PointsAmount,
            BalanceAfter = transaction.BalanceAfter,
            AmountEuros = transaction.AmountEuros,
            Status = "completed",
            CreatedAtUtc = transaction.CreatedAtUtc
        }).ToArray();
    }

    private async Task<IReadOnlyCollection<UserActivityItemResponse>> GetTicketActivitiesAsync(
        Guid userId,
        Guid? negocioId,
        CancellationToken cancellationToken)
    {
        IQueryable<Ticket> query = _fidelityDbContext.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.UserId == userId);

        if (negocioId.HasValue)
        {
            query = query.Where(ticket => ticket.NegocioId == negocioId.Value);
        }

        List<Ticket> tickets = await query
            .OrderByDescending(ticket => ticket.UpdatedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken);

        Dictionary<Guid, UserActivityBusinessSummaryResponse> negocios =
            await GetBusinessSummariesAsync(tickets.Select(ticket => ticket.NegocioId), cancellationToken);

        List<UserActivityItemResponse> activities = [];
        DateTime now = DateTime.UtcNow;

        foreach (Ticket ticket in tickets)
        {
            UserActivityBusinessSummaryResponse? negocio = negocios.GetValueOrDefault(ticket.NegocioId);

            activities.Add(new UserActivityItemResponse
            {
                Id = $"ticket-assigned:{ticket.Id:N}",
                Category = "tickets",
                Type = "TicketAssigned",
                Title = "Ticket recibido",
                Description = ticket.Nombre,
                Negocio = negocio,
                TicketId = ticket.Id,
                Status = ResolveTicketStatus(ticket, now),
                CreatedAtUtc = ticket.CreatedAtUtc
            });

            if (ticket.UsedAtUtc.HasValue)
            {
                activities.Add(new UserActivityItemResponse
                {
                    Id = $"ticket-redeemed:{ticket.Id:N}",
                    Category = "tickets",
                    Type = "TicketRedeemed",
                    Title = "Ticket canjeado",
                    Description = ticket.Nombre,
                    Negocio = negocio,
                    TicketId = ticket.Id,
                    Status = "redeemed",
                    CreatedAtUtc = ticket.UsedAtUtc.Value
                });
            }
            else if (ticket.ExpiresAtUtc <= now)
            {
                activities.Add(new UserActivityItemResponse
                {
                    Id = $"ticket-expired:{ticket.Id:N}",
                    Category = "tickets",
                    Type = "TicketExpired",
                    Title = "Ticket caducado",
                    Description = ticket.Nombre,
                    Negocio = negocio,
                    TicketId = ticket.Id,
                    Status = "expired",
                    CreatedAtUtc = ticket.ExpiresAtUtc
                });
            }
        }

        return activities;
    }

    private async Task<Dictionary<Guid, UserActivityBusinessSummaryResponse>> GetBusinessSummariesAsync(
        IEnumerable<Guid> negocioIds,
        CancellationToken cancellationToken)
    {
        Guid[] ids = negocioIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        HashSet<Guid> idSet = ids.ToHashSet();
        return (await _negociosDbContext.Negocios
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .Where(negocio => idSet.Contains(negocio.Id))
            .Select(negocio => new UserActivityBusinessSummaryResponse
            {
                Id = negocio.Id,
                Nombre = negocio.NombreComercial,
                Slug = negocio.SlugPortal,
                LogoUrl = negocio.LogoPrincipalUrl,
                IconoUrl = negocio.IconoUrl
            })
            .ToDictionary(negocio => negocio.Id);
    }

    private static string? NormalizeCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return "core";
        }

        string normalized = category.Trim().ToLowerInvariant();
        return normalized is "core" or "auth" or "points" or "tickets" ? normalized : "core";
    }

    private static string ResolveAuthTitle(AuthEventType eventType, bool succeeded)
        => eventType switch
        {
            AuthEventType.RegisterStarted => "Registro iniciado",
            AuthEventType.RegisterCompleted => "Registro completado",
            AuthEventType.LoginSucceeded => "Inicio de sesión",
            AuthEventType.LoginFailed => "Intento de inicio de sesión fallido",
            AuthEventType.RefreshSucceeded => "Sesión renovada",
            AuthEventType.RefreshFailed => "Renovación de sesión fallida",
            AuthEventType.Logout => "Sesión cerrada",
            AuthEventType.PasswordChanged => "Contraseña actualizada",
            AuthEventType.ClassicRegisterCreated => "Cuenta creada",
            _ => succeeded ? "Actividad de cuenta" : "Actividad fallida"
        };

    private static string ResolvePointsTitle(PointsTransactionType transactionType, int pointsAmount)
        => transactionType switch
        {
            PointsTransactionType.Earn => $"+{pointsAmount} puntos",
            PointsTransactionType.BackofficeEarn => $"+{pointsAmount} puntos",
            PointsTransactionType.TransferIn => $"+{pointsAmount} puntos recibidos",
            PointsTransactionType.Spend => $"{pointsAmount} puntos gastados",
            PointsTransactionType.TransferOut => $"{pointsAmount} puntos enviados",
            _ => "Movimiento de puntos"
        };

    private static string ResolveTransactionDirection(PointsTransactionType transactionType)
        => transactionType is PointsTransactionType.Earn or PointsTransactionType.BackofficeEarn or PointsTransactionType.TransferIn
            ? "in"
            : "out";

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

        return ticket.ExpiresAtUtc <= now ? "expired" : "active";
    }

    private Guid? GetCurrentUserId()
    {
        string? value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out Guid userId) ? userId : null;
    }
}
