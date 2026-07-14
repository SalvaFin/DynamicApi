using System.Security.Claims;
using System.Data.Common;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Domain.Enums;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Dynamic.Negocios.Domain.Entities;
using Dynamic.Negocios.Domain.Enums;
using Dynamic.Negocios.Infrastructure.Persistence;
using Dynamic.Promotions.Domain.Enums;
using Dynamic.Promotions.Infrastructure.Persistence;
using Dynamic.Users.Domain.Entities;
using Dynamic.Users.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DynamicApi.Controllers;

[ApiController]
[Authorize(Policy = "BusinessStaffAuth")]
[Route("api/backoffice/negocios/{negocioId:guid}/analytics")]
public class BusinessAnalyticsController : ControllerBase
{
    private readonly DynamicFidelityDbContext _fidelityDbContext;
    private readonly DynamicNegociosDbContext _negociosDbContext;
    private readonly DynamicPromotionsDbContext _promotionsDbContext;
    private readonly DynamicUsersDbContext _usersDbContext;
    private readonly ILogger<BusinessAnalyticsController> _logger;

    public BusinessAnalyticsController(
        DynamicFidelityDbContext fidelityDbContext,
        DynamicNegociosDbContext negociosDbContext,
        DynamicPromotionsDbContext promotionsDbContext,
        DynamicUsersDbContext usersDbContext,
        ILogger<BusinessAnalyticsController> logger)
    {
        _fidelityDbContext = fidelityDbContext;
        _negociosDbContext = negociosDbContext;
        _promotionsDbContext = promotionsDbContext;
        _usersDbContext = usersDbContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        Guid negocioId,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (TryResolveRange(fromUtc, toUtc, out AnalyticsDateRange range, out IActionResult? rangeError) is false)
        {
            return rangeError!;
        }

        IActionResult? authorization = await AuthorizeAnalyticsAsync(negocioId, cancellationToken);
        if (authorization is not null)
        {
            return authorization;
        }

        List<AnalyticsWarningResponse> warnings = [];

        return Ok(new BusinessAnalyticsResponse(
            range,
            await SafeBuildSectionAsync("overview", negocioId, range, BuildOverviewAsync, EmptyOverview, warnings, cancellationToken),
            await SafeBuildSectionAsync("acquisition", negocioId, range, BuildAcquisitionAsync, EmptyAcquisition, warnings, cancellationToken),
            await SafeBuildSectionAsync("tickets", negocioId, range, BuildTicketsAsync, EmptyTickets, warnings, cancellationToken),
            await SafeBuildSectionAsync("moneyPoints", negocioId, range, BuildMoneyAndPointsAsync, EmptyMoneyAndPoints, warnings, cancellationToken),
            await SafeBuildSectionAsync("promotions", negocioId, range, BuildPromotionsAsync, EmptyPromotions, warnings, cancellationToken),
            await SafeBuildSectionAsync("operations", negocioId, range, BuildOperationsAsync, EmptyOperations, warnings, cancellationToken),
            warnings));
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview(Guid negocioId, [FromQuery] DateTime? fromUtc = null, [FromQuery] DateTime? toUtc = null, CancellationToken cancellationToken = default)
        => await GetSectionAsync(negocioId, fromUtc, toUtc, BuildOverviewAsync, cancellationToken);

    [HttpGet("acquisition")]
    public async Task<IActionResult> GetAcquisition(Guid negocioId, [FromQuery] DateTime? fromUtc = null, [FromQuery] DateTime? toUtc = null, CancellationToken cancellationToken = default)
        => await GetSectionAsync(negocioId, fromUtc, toUtc, BuildAcquisitionAsync, cancellationToken);

    [HttpGet("tickets")]
    public async Task<IActionResult> GetTickets(Guid negocioId, [FromQuery] DateTime? fromUtc = null, [FromQuery] DateTime? toUtc = null, CancellationToken cancellationToken = default)
        => await GetSectionAsync(negocioId, fromUtc, toUtc, BuildTicketsAsync, cancellationToken);

    [HttpGet("money-points")]
    public async Task<IActionResult> GetMoneyAndPoints(Guid negocioId, [FromQuery] DateTime? fromUtc = null, [FromQuery] DateTime? toUtc = null, CancellationToken cancellationToken = default)
        => await GetSectionAsync(negocioId, fromUtc, toUtc, BuildMoneyAndPointsAsync, cancellationToken);

    [HttpGet("promotions")]
    public async Task<IActionResult> GetPromotions(Guid negocioId, [FromQuery] DateTime? fromUtc = null, [FromQuery] DateTime? toUtc = null, CancellationToken cancellationToken = default)
        => await GetSectionAsync(negocioId, fromUtc, toUtc, BuildPromotionsAsync, cancellationToken);

    [HttpGet("operations")]
    public async Task<IActionResult> GetOperations(Guid negocioId, [FromQuery] DateTime? fromUtc = null, [FromQuery] DateTime? toUtc = null, CancellationToken cancellationToken = default)
        => await GetSectionAsync(negocioId, fromUtc, toUtc, BuildOperationsAsync, cancellationToken);

    private async Task<IActionResult> GetSectionAsync<T>(
        Guid negocioId,
        DateTime? fromUtc,
        DateTime? toUtc,
        Func<Guid, AnalyticsDateRange, CancellationToken, Task<T>> builder,
        CancellationToken cancellationToken)
    {
        if (TryResolveRange(fromUtc, toUtc, out AnalyticsDateRange range, out IActionResult? rangeError) is false)
        {
            return rangeError!;
        }

        IActionResult? authorization = await AuthorizeAnalyticsAsync(negocioId, cancellationToken);
        if (authorization is not null)
        {
            return authorization;
        }

        try
        {
            return Ok(await builder(negocioId, range, cancellationToken));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Error building analytics section for negocio {NegocioId}.", negocioId);
            return StatusCode(StatusCodes.Status500InternalServerError, new AnalyticsSectionErrorResponse(
                Section: typeof(T).Name,
                ExceptionType: exception.GetType().Name,
                Message: exception.Message));
        }
    }

    private async Task<T> SafeBuildSectionAsync<T>(
        string section,
        Guid negocioId,
        AnalyticsDateRange range,
        Func<Guid, AnalyticsDateRange, CancellationToken, Task<T>> builder,
        Func<AnalyticsDateRange, T> fallback,
        ICollection<AnalyticsWarningResponse> warnings,
        CancellationToken cancellationToken)
    {
        try
        {
            return await builder(negocioId, range, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Error building analytics section {Section} for negocio {NegocioId}.", section, negocioId);
            warnings.Add(new AnalyticsWarningResponse(section, exception.GetType().Name, exception.Message));
            return fallback(range);
        }
    }

    private async Task<AnalyticsOverviewResponse> BuildOverviewAsync(
        Guid negocioId,
        AnalyticsDateRange range,
        CancellationToken cancellationToken)
    {
        List<CustomerLinkProjection> customerLinks = await GetCustomerLinksAsync(negocioId, range, cancellationToken);
        int newCustomers = customerLinks.Select(link => link.UserId).Distinct().Count();

        int activeCustomers = await GetActiveCustomerCountAsync(negocioId, range, cancellationToken);
        int returningCustomers = await GetReturningCustomerCountAsync(negocioId, range, cancellationToken);

        decimal pointsTrackedRevenue = await PointsTransactions(negocioId, range)
            .Where(transaction =>
                transaction.TransactionType == PointsTransactionType.Earn ||
                transaction.TransactionType == PointsTransactionType.BackofficeEarn)
            .SumAsync(transaction => transaction.AmountEuros ?? 0m, cancellationToken);

        int pointsIssued = await PointsTransactions(negocioId, range)
            .Where(transaction =>
                transaction.TransactionType == PointsTransactionType.Earn ||
                transaction.TransactionType == PointsTransactionType.BackofficeEarn ||
                transaction.TransactionType == PointsTransactionType.TransferIn)
            .SumAsync(transaction => transaction.PointsAmount, cancellationToken);

        List<TicketRedemption> ticketRedemptions = await GetTicketRedemptionsAsync(negocioId, range, cancellationToken);
        decimal ticketPurchaseAmount = ticketRedemptions.Sum(redemption => redemption.PurchaseAmount ?? 0m);
        decimal ticketDiscountAmount = ticketRedemptions.Sum(redemption => redemption.DiscountAmount ?? 0m);

        int assignedTickets = await AssignedTickets(negocioId)
            .Where(ticket => ticket.CreatedAtUtc >= range.FromUtc && ticket.CreatedAtUtc < range.ToUtc)
            .CountAsync(cancellationToken);

        int redeemedTickets = await CountTicketRedemptionsIncludingLegacyAsync(negocioId, range, ticketRedemptions, cancellationToken);
        decimal redemptionRate = assignedTickets == 0 ? 0 : decimal.Round((decimal)redeemedTickets / assignedTickets * 100m, 2);

        int sentCampaigns = await _promotionsDbContext.Campaigns
            .AsNoTracking()
            .Where(campaign =>
                campaign.NegocioId == negocioId &&
                campaign.CreatedAtUtc >= range.FromUtc &&
                campaign.CreatedAtUtc < range.ToUtc &&
                campaign.Status == PromotionCampaignStatus.Sent)
            .CountAsync(cancellationToken);

        return new AnalyticsOverviewResponse(
            range,
            [
                new("newCustomers", "Clientes captados", newCustomers, "count", "Clientes nuevos vinculados al negocio en el periodo."),
                new("activeCustomers", "Clientes activos", activeCustomers, "count", "Clientes con actividad de puntos, tickets o captacion en el periodo."),
                new("returningCustomers", "Clientes recurrentes", returningCustomers, "count", "Clientes con mas de una actividad trazable en el periodo."),
                new("pointsTrackedRevenue", "Euros trazados por puntos", pointsTrackedRevenue, "eur", "Importe de compras validadas para generar puntos."),
                new("ticketPurchaseAmount", "Euros con ticket", ticketPurchaseAmount, "eur", "Importe de compra guardado en canjes de tickets instrumentados."),
                new("ticketsRedeemed", "Tickets canjeados", redeemedTickets, "count", "Canjes de tickets, incluyendo historico previo sin detalle economico."),
                new("redemptionRate", "Ratio de canje", redemptionRate, "percent", "Tickets canjeados frente a tickets asignados en el periodo."),
                new("pointsIssued", "Puntos emitidos", pointsIssued, "points", "Puntos entregados a clientes en el periodo."),
                new("sentCampaigns", "Campañas enviadas", sentCampaigns, "count", "Campañas promocionales marcadas como enviadas.")
            ],
            new TicketRevenueInstrumentationResponse(
                IsInstrumented: true,
                StartedWithThisVersion: true,
                PurchaseAmount: ticketPurchaseAmount,
                DiscountAmount: ticketDiscountAmount,
                Note: "Los canjes nuevos guardan importe de compra, descuento e importe final. Los canjes historicos previos no tienen detalle economico."));
    }

    private static AnalyticsOverviewResponse EmptyOverview(AnalyticsDateRange range)
        => new(
            range,
            [
                new("newCustomers", "Clientes captados", 0, "count", "No disponible temporalmente."),
                new("activeCustomers", "Clientes activos", 0, "count", "No disponible temporalmente."),
                new("returningCustomers", "Clientes recurrentes", 0, "count", "No disponible temporalmente."),
                new("pointsTrackedRevenue", "Euros trazados por puntos", 0, "eur", "No disponible temporalmente."),
                new("ticketPurchaseAmount", "Euros con ticket", 0, "eur", "No disponible temporalmente."),
                new("ticketsRedeemed", "Tickets canjeados", 0, "count", "No disponible temporalmente."),
                new("redemptionRate", "Ratio de canje", 0, "percent", "No disponible temporalmente."),
                new("pointsIssued", "Puntos emitidos", 0, "points", "No disponible temporalmente."),
                new("sentCampaigns", "Campanas enviadas", 0, "count", "No disponible temporalmente.")
            ],
            new(false, false, 0, 0, "La seccion overview no se pudo calcular."));

    private async Task<AcquisitionAnalyticsResponse> BuildAcquisitionAsync(
        Guid negocioId,
        AnalyticsDateRange range,
        CancellationToken cancellationToken)
    {
        List<CustomerLinkProjection> links = (await GetCustomerLinksAsync(negocioId, range, cancellationToken))
            .OrderByDescending(link => link.CreatedAtUtc)
            .ToList();

        List<PendingTicketAssignment> qrAssignments = await _fidelityDbContext.PendingTicketAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.NegocioId == negocioId &&
                assignment.Activated &&
                assignment.ActivatedAtUtc >= range.FromUtc &&
                assignment.ActivatedAtUtc < range.ToUtc)
            .ToListAsync(cancellationToken);

        List<AnalyticsBreakdownItemResponse> byOrigin = links
            .GroupBy(link => NormalizeOrigin(link.Origin))
            .Select(group => new AnalyticsBreakdownItemResponse(group.Key, ResolveOriginLabel(group.Key), group.Select(link => link.UserId).Distinct().Count()))
            .OrderByDescending(item => item.Value)
            .ToList();

        if (qrAssignments.Count > 0)
        {
            byOrigin.Add(new AnalyticsBreakdownItemResponse("qr_ticket", "QR de bienvenida", qrAssignments.Select(assignment => assignment.UserId).Distinct().Count()));
        }

        Guid[] latestUserIds = links.Take(20).Select(link => link.UserId).Distinct().ToArray();
        Dictionary<Guid, UserAccount> users = await _usersDbContext.Users
            .AsNoTracking()
            .Where(user => latestUserIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, cancellationToken);

        CustomerAcquisitionItemResponse[] latestCustomers = links
            .Take(20)
            .Select(link => new CustomerAcquisitionItemResponse(
                link.UserId,
                ResolveUserDisplayName(users.GetValueOrDefault(link.UserId)),
                NormalizeOrigin(link.Origin),
                ResolveOriginLabel(NormalizeOrigin(link.Origin)),
                link.CreatedAtUtc,
                qrAssignments.Any(assignment => assignment.UserId == link.UserId)))
            .ToArray();

        int customersWithFirstTicket = await AssignedTickets(negocioId)
            .Where(ticket =>
                ticket.CategoriaEnvioEspecial == CategoriaEnvioTicket.PrimerRegistro &&
                ticket.CreatedAtUtc >= range.FromUtc &&
                ticket.CreatedAtUtc < range.ToUtc)
            .Select(ticket => ticket.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        int customersWithActivity = await GetActiveCustomerCountAsync(negocioId, range, cancellationToken);

        return new AcquisitionAnalyticsResponse(
            range,
            TotalNewCustomers: links.Select(link => link.UserId).Distinct().Count(),
            CustomersWithWelcomeTicket: customersWithFirstTicket,
            CustomersWithActivity: customersWithActivity,
            ByOrigin: byOrigin.OrderByDescending(item => item.Value).ToArray(),
            LatestCustomers: latestCustomers);
    }

    private static AcquisitionAnalyticsResponse EmptyAcquisition(AnalyticsDateRange range)
        => new(range, 0, 0, 0, [], []);

    private async Task<TicketsAnalyticsResponse> BuildTicketsAsync(
        Guid negocioId,
        AnalyticsDateRange range,
        CancellationToken cancellationToken)
    {
        int assigned = await AssignedTickets(negocioId)
            .Where(ticket => ticket.CreatedAtUtc >= range.FromUtc && ticket.CreatedAtUtc < range.ToUtc)
            .CountAsync(cancellationToken);

        List<TicketRedemption> redemptions = await GetTicketRedemptionsAsync(negocioId, range, cancellationToken);
        int redeemed = await CountTicketRedemptionsIncludingLegacyAsync(negocioId, range, redemptions, cancellationToken);

        int active = await AssignedTickets(negocioId)
            .Where(ticket => ticket.Activo && !ticket.Usado && ticket.ExpiresAtUtc > range.ToUtc)
            .CountAsync(cancellationToken);

        int expired = await AssignedTickets(negocioId)
            .Where(ticket => !ticket.Usado && ticket.ExpiresAtUtc >= range.FromUtc && ticket.ExpiresAtUtc < range.ToUtc)
            .CountAsync(cancellationToken);

        List<Ticket> assignedTickets = await AssignedTickets(negocioId)
            .Where(ticket => ticket.CreatedAtUtc >= range.FromUtc && ticket.CreatedAtUtc < range.ToUtc)
            .ToListAsync(cancellationToken);

        AnalyticsBreakdownItemResponse[] byCategory = assignedTickets
            .GroupBy(ticket => ticket.CategoriaEnvioEspecial.ToString())
            .Select(group => new AnalyticsBreakdownItemResponse(group.Key, ResolveTicketCategoryLabel(group.Key), group.Count()))
            .OrderByDescending(item => item.Value)
            .ToArray();

        AnalyticsBreakdownItemResponse[] bySource =
        [
            new("qr", "QR", assignedTickets.Count(ticket => ticket.SourceQrCampaignId.HasValue)),
            new("promotion", "Promocion", assignedTickets.Count(ticket => ticket.SourcePromotionCampaignId.HasValue)),
            new("points_unlock", "Desbloqueo con puntos", assignedTickets.Count(ticket => ticket.CategoriaEnvioEspecial == CategoriaEnvioTicket.General && ticket.PuntosCoste.GetValueOrDefault() > 0)),
            new("welcome", "Bienvenida", assignedTickets.Count(ticket => ticket.CategoriaEnvioEspecial == CategoriaEnvioTicket.PrimerRegistro)),
            new("referral", "Referido", assignedTickets.Count(ticket => ticket.CategoriaEnvioEspecial == CategoriaEnvioTicket.InvitacionClienteNuevo))
        ];

        TicketTemplateMetricResponse[] topTickets = assignedTickets
            .GroupBy(ticket => new
            {
                TemplateId = ticket.ParentTicketId ?? ticket.Id,
                ticket.Nombre,
                Tipo = ticket.Tipo.ToString(),
                Categoria = ticket.CategoriaEnvioEspecial.ToString(),
                ticket.Valor,
                ticket.PuntosCoste
            })
            .Select(group =>
            {
                Guid templateId = group.Key.TemplateId;
                int templateRedemptions = redemptions.Count(redemption => (redemption.ParentTicketId ?? redemption.TicketId) == templateId);
                return new TicketTemplateMetricResponse(
                    templateId,
                    group.Key.Nombre,
                    group.Key.Tipo,
                    group.Key.Categoria,
                    group.Count(),
                    templateRedemptions,
                    group.Count() == 0 ? 0 : decimal.Round((decimal)templateRedemptions / group.Count() * 100m, 2),
                    group.Key.Valor,
                    group.Key.PuntosCoste);
            })
            .OrderByDescending(item => item.RedeemedCount)
            .ThenByDescending(item => item.AssignedCount)
            .Take(10)
            .ToArray();

        return new TicketsAnalyticsResponse(
            range,
            AssignedCount: assigned,
            RedeemedCount: redeemed,
            ActiveCount: active,
            ExpiredCount: expired,
            RedemptionRate: assigned == 0 ? 0 : decimal.Round((decimal)redeemed / assigned * 100m, 2),
            ByCategory: byCategory,
            BySource: bySource,
            TopTickets: topTickets);
    }

    private static TicketsAnalyticsResponse EmptyTickets(AnalyticsDateRange range)
        => new(range, 0, 0, 0, 0, 0, [], [], []);

    private async Task<MoneyAndPointsAnalyticsResponse> BuildMoneyAndPointsAsync(
        Guid negocioId,
        AnalyticsDateRange range,
        CancellationToken cancellationToken)
    {
        List<PointsTransaction> transactions = await PointsTransactions(negocioId, range)
            .ToListAsync(cancellationToken);

        List<TicketRedemption> redemptions = await GetTicketRedemptionsAsync(negocioId, range, cancellationToken);

        decimal pointsTrackedRevenue = transactions
            .Where(transaction => transaction.TransactionType is PointsTransactionType.Earn or PointsTransactionType.BackofficeEarn)
            .Sum(transaction => transaction.AmountEuros ?? 0m);

        decimal ticketPurchaseAmount = redemptions.Sum(redemption => redemption.PurchaseAmount ?? 0m);
        decimal ticketDiscountAmount = redemptions.Sum(redemption => redemption.DiscountAmount ?? 0m);
        decimal ticketFinalAmount = redemptions.Sum(redemption => redemption.FinalAmount ?? 0m);

        int pointsIssued = transactions
            .Where(transaction => transaction.TransactionType is PointsTransactionType.Earn or PointsTransactionType.BackofficeEarn or PointsTransactionType.TransferIn)
            .Sum(transaction => transaction.PointsAmount);

        int pointsSpent = transactions
            .Where(transaction => transaction.TransactionType is PointsTransactionType.Spend or PointsTransactionType.TransferOut)
            .Sum(transaction => transaction.PointsAmount);

        int livePointsBalance = await _fidelityDbContext.Points
            .AsNoTracking()
            .Where(points => points.NegocioId == negocioId)
            .SumAsync(points => points.CurrentBalance, cancellationToken);

        CustomerValueMetricResponse[] topCustomers = transactions
            .GroupBy(transaction => transaction.UserId)
            .Select(group => new CustomerValueMetricResponse(
                group.Key,
                group.Sum(transaction => transaction.AmountEuros ?? 0m),
                group.Sum(transaction => transaction.TransactionType is PointsTransactionType.Earn or PointsTransactionType.BackofficeEarn ? transaction.PointsAmount : 0),
                group.Count(),
                group.Max(transaction => transaction.CreatedAtUtc)))
            .OrderByDescending(item => item.TrackedRevenue)
            .ThenByDescending(item => item.ActivityCount)
            .Take(10)
            .ToArray();

        return new MoneyAndPointsAnalyticsResponse(
            range,
            PointsTrackedRevenue: pointsTrackedRevenue,
            TicketPurchaseAmount: ticketPurchaseAmount,
            TicketDiscountAmount: ticketDiscountAmount,
            TicketFinalAmount: ticketFinalAmount,
            PointsIssued: pointsIssued,
            PointsSpent: pointsSpent,
            LivePointsBalance: livePointsBalance,
            AverageTrackedTicket: transactions.Count(transaction => transaction.AmountEuros.HasValue) == 0
                ? 0
                : decimal.Round(pointsTrackedRevenue / transactions.Count(transaction => transaction.AmountEuros.HasValue), 2),
            TopCustomers: topCustomers);
    }

    private static MoneyAndPointsAnalyticsResponse EmptyMoneyAndPoints(AnalyticsDateRange range)
        => new(range, 0, 0, 0, 0, 0, 0, 0, 0, []);

    private async Task<PromotionsAnalyticsResponse> BuildPromotionsAsync(
        Guid negocioId,
        AnalyticsDateRange range,
        CancellationToken cancellationToken)
    {
        var campaigns = await _promotionsDbContext.Campaigns
            .AsNoTracking()
            .Where(campaign =>
                campaign.NegocioId == negocioId &&
                campaign.CreatedAtUtc >= range.FromUtc &&
                campaign.CreatedAtUtc < range.ToUtc)
            .OrderByDescending(campaign => campaign.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        Guid[] campaignIds = campaigns.Select(campaign => campaign.Id).ToArray();

        List<Ticket> campaignTickets = await AssignedTickets(negocioId)
            .Where(ticket => ticket.SourcePromotionCampaignId.HasValue && campaignIds.Contains(ticket.SourcePromotionCampaignId.Value))
            .ToListAsync(cancellationToken);

        List<TicketRedemption> campaignRedemptions = (await GetTicketRedemptionsAsync(
                negocioId,
                range,
                cancellationToken))
            .Where(redemption =>
                redemption.SourcePromotionCampaignId.HasValue &&
                campaignIds.Contains(redemption.SourcePromotionCampaignId.Value))
            .ToList();

        CampaignMetricResponse[] campaignMetrics = campaigns
            .Select(campaign =>
            {
                int generatedTickets = campaignTickets.Count(ticket => ticket.SourcePromotionCampaignId == campaign.Id);
                int redeemedTickets = campaignRedemptions.Count(redemption => redemption.SourcePromotionCampaignId == campaign.Id);
                return new CampaignMetricResponse(
                    campaign.Id,
                    campaign.TicketNombreSnapshot,
                    campaign.Status.ToString(),
                    campaign.AudienceCount,
                    campaign.PushEligibleCount,
                    campaign.PushDeliveredCount,
                    campaign.PushFailedCount,
                    generatedTickets,
                    redeemedTickets,
                    generatedTickets == 0 ? 0 : decimal.Round((decimal)redeemedTickets / generatedTickets * 100m, 2),
                    campaign.CreatedAtUtc,
                    campaign.StartsAtUtc,
                    campaign.ExpiresAtUtc);
            })
            .ToArray();

        return new PromotionsAnalyticsResponse(
            range,
            CampaignCount: campaigns.Count,
            AudienceCount: campaigns.Sum(campaign => campaign.AudienceCount),
            PushEligibleCount: campaigns.Sum(campaign => campaign.PushEligibleCount),
            PushDeliveredCount: campaigns.Sum(campaign => campaign.PushDeliveredCount),
            PushFailedCount: campaigns.Sum(campaign => campaign.PushFailedCount),
            TicketsGeneratedCount: campaignTickets.Count,
            TicketsRedeemedCount: campaignRedemptions.Count,
            Campaigns: campaignMetrics);
    }

    private static PromotionsAnalyticsResponse EmptyPromotions(AnalyticsDateRange range)
        => new(range, 0, 0, 0, 0, 0, 0, 0, []);

    private async Task<OperationsAnalyticsResponse> BuildOperationsAsync(
        Guid negocioId,
        AnalyticsDateRange range,
        CancellationToken cancellationToken)
    {
        int completedPointOperations = await _fidelityDbContext.PointsOperations
            .AsNoTracking()
            .Where(operation =>
                operation.NegocioId == negocioId &&
                operation.Status == PointsOperationStatus.Completed &&
                operation.ValidatedAtUtc >= range.FromUtc &&
                operation.ValidatedAtUtc < range.ToUtc)
            .CountAsync(cancellationToken);

        int cancelledPointOperations = await _fidelityDbContext.PointsOperations
            .AsNoTracking()
            .Where(operation =>
                operation.NegocioId == negocioId &&
                operation.Status == PointsOperationStatus.Cancelled &&
                operation.CancelledAtUtc >= range.FromUtc &&
                operation.CancelledAtUtc < range.ToUtc)
            .CountAsync(cancellationToken);

        int failedPinAttempts = await _fidelityDbContext.PointsOperationAttempts
            .AsNoTracking()
            .Where(attempt =>
                attempt.NegocioId == negocioId &&
                !attempt.Succeeded &&
                attempt.CreatedAtUtc >= range.FromUtc &&
                attempt.CreatedAtUtc < range.ToUtc)
            .CountAsync(cancellationToken);

        List<TicketRedemption> redemptions = await GetTicketRedemptionsAsync(negocioId, range, cancellationToken);

        ValidatorMetricResponse[] topValidators = redemptions
            .GroupBy(redemption => redemption.ValidatedByUserId)
            .Select(group => new ValidatorMetricResponse(
                group.Key,
                TicketRedemptions: group.Count(),
                TicketPurchaseAmount: group.Sum(redemption => redemption.PurchaseAmount ?? 0m),
                LastActivityAtUtc: group.Max(redemption => redemption.CreatedAtUtc)))
            .OrderByDescending(item => item.TicketRedemptions)
            .ThenByDescending(item => item.TicketPurchaseAmount)
            .Take(10)
            .ToArray();

        return new OperationsAnalyticsResponse(
            range,
            CompletedPointOperations: completedPointOperations,
            CancelledPointOperations: cancelledPointOperations,
            FailedPinAttempts: failedPinAttempts,
            TicketValidationCount: redemptions.Count,
            TopValidators: topValidators);
    }

    private static OperationsAnalyticsResponse EmptyOperations(AnalyticsDateRange range)
        => new(range, 0, 0, 0, 0, []);

    private async Task<List<CustomerLinkProjection>> GetCustomerLinksAsync(
        Guid negocioId,
        AnalyticsDateRange range,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _negociosDbContext.NegociosAudiencias
                .AsNoTracking()
                .Where(link =>
                    link.NegocioId == negocioId &&
                    link.CreatedAtUtc >= range.FromUtc &&
                    link.CreatedAtUtc < range.ToUtc)
                .Select(link => new CustomerLinkProjection(
                    link.UserId,
                    link.OrigenAlta,
                    link.CreatedAtUtc))
                .ToListAsync(cancellationToken);
        }
        catch (DbException)
        {
            return await _negociosDbContext.NegociosUsuariosVinculaciones
                .AsNoTracking()
                .Where(link =>
                    link.NegocioId == negocioId &&
                    link.TipoVinculacion == TipoVinculacionNegocioUsuario.Cliente &&
                    link.CreatedAtUtc >= range.FromUtc &&
                    link.CreatedAtUtc < range.ToUtc)
                .Select(link => new CustomerLinkProjection(
                    link.UserId,
                    link.OrigenVinculacion,
                    link.CreatedAtUtc))
                .ToListAsync(cancellationToken);
        }
    }

    private IQueryable<Ticket> AssignedTickets(Guid negocioId)
        => _fidelityDbContext.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.NegocioId == negocioId && !ticket.EsPlantilla && ticket.UserId.HasValue);

    private async Task<List<TicketRedemption>> GetTicketRedemptionsAsync(
        Guid negocioId,
        AnalyticsDateRange range,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _fidelityDbContext.TicketRedemptions
                .AsNoTracking()
                .Where(redemption =>
                    redemption.NegocioId == negocioId &&
                    redemption.CreatedAtUtc >= range.FromUtc &&
                    redemption.CreatedAtUtc < range.ToUtc)
                .ToListAsync(cancellationToken);
        }
        catch (DbException)
        {
            return [];
        }
    }

    private IQueryable<PointsTransaction> PointsTransactions(Guid negocioId, AnalyticsDateRange range)
        => _fidelityDbContext.PointsTransactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.NegocioId == negocioId &&
                transaction.CreatedAtUtc >= range.FromUtc &&
                transaction.CreatedAtUtc < range.ToUtc);

    private async Task<int> CountTicketRedemptionsIncludingLegacyAsync(
        Guid negocioId,
        AnalyticsDateRange range,
        IReadOnlyCollection<TicketRedemption> instrumentedRedemptions,
        CancellationToken cancellationToken)
    {
        Guid[] instrumentedTicketIds = instrumentedRedemptions
            .Select(redemption => redemption.TicketId)
            .Distinct()
            .ToArray();

        int legacyRedemptions = await AssignedTickets(negocioId)
            .Where(ticket =>
                ticket.UsedAtUtc >= range.FromUtc &&
                ticket.UsedAtUtc < range.ToUtc &&
                !instrumentedTicketIds.Contains(ticket.Id))
            .CountAsync(cancellationToken);

        return instrumentedRedemptions.Count + legacyRedemptions;
    }

    private async Task<int> GetActiveCustomerCountAsync(
        Guid negocioId,
        AnalyticsDateRange range,
        CancellationToken cancellationToken)
    {
        Guid[] pointsUsers = await PointsTransactions(negocioId, range)
            .Select(transaction => transaction.UserId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        Guid[] ticketUsers = (await GetTicketRedemptionsAsync(negocioId, range, cancellationToken))
            .Select(redemption => redemption.UserId)
            .Distinct()
            .ToArray();

        Guid[] legacyTicketUsers = await AssignedTickets(negocioId)
            .Where(ticket =>
                (ticket.CreatedAtUtc >= range.FromUtc && ticket.CreatedAtUtc < range.ToUtc) ||
                (ticket.UpdatedAtUtc >= range.FromUtc && ticket.UpdatedAtUtc < range.ToUtc) ||
                (ticket.UsedAtUtc >= range.FromUtc && ticket.UsedAtUtc < range.ToUtc))
            .Select(ticket => ticket.UserId!.Value)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        Guid[] linkedUsers = (await GetCustomerLinksAsync(negocioId, range, cancellationToken))
            .Select(link => link.UserId)
            .Distinct()
            .ToArray();

        return pointsUsers
            .Concat(ticketUsers)
            .Concat(legacyTicketUsers)
            .Concat(linkedUsers)
            .Distinct()
            .Count();
    }

    private async Task<int> GetReturningCustomerCountAsync(
        Guid negocioId,
        AnalyticsDateRange range,
        CancellationToken cancellationToken)
    {
        Guid[] recurringPointUsers = await PointsTransactions(negocioId, range)
            .GroupBy(transaction => transaction.UserId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArrayAsync(cancellationToken);

        Guid[] recurringTicketUsers = (await GetTicketRedemptionsAsync(negocioId, range, cancellationToken))
            .GroupBy(redemption => redemption.UserId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        Guid[] recurringLegacyTicketUsers = await AssignedTickets(negocioId)
            .Where(ticket =>
                (ticket.CreatedAtUtc >= range.FromUtc && ticket.CreatedAtUtc < range.ToUtc) ||
                (ticket.UpdatedAtUtc >= range.FromUtc && ticket.UpdatedAtUtc < range.ToUtc) ||
                (ticket.UsedAtUtc >= range.FromUtc && ticket.UsedAtUtc < range.ToUtc))
            .GroupBy(ticket => ticket.UserId!.Value)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArrayAsync(cancellationToken);

        return recurringPointUsers
            .Concat(recurringTicketUsers)
            .Concat(recurringLegacyTicketUsers)
            .Distinct()
            .Count();
    }

    private async Task<IActionResult?> AuthorizeAnalyticsAsync(Guid negocioId, CancellationToken cancellationToken)
    {
        bool negocioExists = await _negociosDbContext.Negocios
            .AsNoTracking()
            .AnyAsync(item => item.Id == negocioId && !item.IsDeleted, cancellationToken);

        if (!negocioExists)
        {
            return NotFound(new { message = "El negocio no existe." });
        }

        if (User.IsInRole("Admin"))
        {
            return null;
        }

        Guid? userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        BusinessLinkProjection? link = await _negociosDbContext.NegociosUsuariosVinculaciones
            .AsNoTracking()
            .Where(item => item.NegocioId == negocioId && item.UserId == userId.Value)
            .Select(item => new BusinessLinkProjection(
                item.TipoVinculacion,
                item.Activa,
                item.PuedeGestionarNegocio,
                item.PuedeGestionarCampanas,
                item.PuedeGestionarPuntos,
                item.PuedeVerReportes,
                item.FechaInicioUtc,
                item.FechaFinUtc,
                item.RevokedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

        if (!IsActiveLink(link))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "El usuario no esta vinculado al negocio." });
        }

        bool canViewAnalytics =
            link!.TipoVinculacion is TipoVinculacionNegocioUsuario.Propietario or TipoVinculacionNegocioUsuario.Gerente ||
            link.PuedeVerReportes ||
            link.PuedeGestionarNegocio ||
            link.PuedeGestionarCampanas ||
            link.PuedeGestionarPuntos;

        return canViewAnalytics
            ? null
            : StatusCode(StatusCodes.Status403Forbidden, new { message = "El usuario no tiene permisos para ver metricas." });
    }

    private static bool IsActiveLink(NegocioUsuarioVinculacion? link)
    {
        if (link is null || !link.Activa || link.RevokedAtUtc.HasValue)
        {
            return false;
        }

        DateTime now = DateTime.UtcNow;
        return (!link.FechaInicioUtc.HasValue || link.FechaInicioUtc.Value <= now) &&
               (!link.FechaFinUtc.HasValue || link.FechaFinUtc.Value >= now);
    }

    private static bool IsActiveLink(BusinessLinkProjection? link)
    {
        if (link is null || !link.Activa || link.RevokedAtUtc.HasValue)
        {
            return false;
        }

        DateTime now = DateTime.UtcNow;
        return (!link.FechaInicioUtc.HasValue || link.FechaInicioUtc.Value <= now) &&
               (!link.FechaFinUtc.HasValue || link.FechaFinUtc.Value >= now);
    }

    private bool TryResolveRange(
        DateTime? fromUtc,
        DateTime? toUtc,
        out AnalyticsDateRange range,
        out IActionResult? error)
    {
        DateTime resolvedTo = NormalizeUtc(toUtc ?? DateTime.UtcNow);
        DateTime resolvedFrom = NormalizeUtc(fromUtc ?? resolvedTo.AddDays(-30));

        if (resolvedFrom >= resolvedTo)
        {
            range = new AnalyticsDateRange(resolvedFrom, resolvedTo);
            error = BadRequest(new { message = "fromUtc debe ser anterior a toUtc." });
            return false;
        }

        range = new AnalyticsDateRange(resolvedFrom, resolvedTo);
        error = null;
        return true;
    }

    private static DateTime NormalizeUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    private Guid? GetCurrentUserId()
    {
        string? value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out Guid userId) ? userId : null;
    }

    private static string NormalizeOrigin(string? origin)
        => string.IsNullOrWhiteSpace(origin) ? "unknown" : origin.Trim().ToLowerInvariant();

    private static string ResolveOriginLabel(string origin)
        => origin switch
        {
            "audience_join" => "Formar parte",
            "business_follow" => "Formar parte",
            "business_staff_customer_register" => "Alta cliente backoffice",
            "welcome_ticket" => "Bono de bienvenida",
            "welcome_ticket_qr" => "QR de bienvenida",
            "points_earn_validation" => "Compra con puntos",
            "points_backoffice_accrual" => "Backoffice puntos",
            "points_backoffice_user_accrual" => "Backoffice puntos",
            "points_direct_add" => "Puntos directos",
            "points_gift" => "Regalo de puntos",
            "qr_ticket" => "QR de bienvenida",
            "unknown" => "Sin origen",
            _ => origin
        };

    private static string ResolveTicketCategoryLabel(string category)
        => category switch
        {
            nameof(CategoriaEnvioTicket.General) => "General",
            nameof(CategoriaEnvioTicket.PrimerRegistro) => "Bienvenida",
            nameof(CategoriaEnvioTicket.InvitacionClienteNuevo) => "Referido",
            _ => category
        };

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
}

public sealed record AnalyticsDateRange(DateTime FromUtc, DateTime ToUtc);

internal sealed record CustomerLinkProjection(Guid UserId, string? Origin, DateTime CreatedAtUtc);

internal sealed record BusinessLinkProjection(
    TipoVinculacionNegocioUsuario TipoVinculacion,
    bool Activa,
    bool PuedeGestionarNegocio,
    bool PuedeGestionarCampanas,
    bool PuedeGestionarPuntos,
    bool PuedeVerReportes,
    DateTime? FechaInicioUtc,
    DateTime? FechaFinUtc,
    DateTime? RevokedAtUtc);

public sealed record BusinessAnalyticsResponse(
    AnalyticsDateRange Range,
    AnalyticsOverviewResponse Overview,
    AcquisitionAnalyticsResponse Acquisition,
    TicketsAnalyticsResponse Tickets,
    MoneyAndPointsAnalyticsResponse MoneyAndPoints,
    PromotionsAnalyticsResponse Promotions,
    OperationsAnalyticsResponse Operations,
    IReadOnlyCollection<AnalyticsWarningResponse> Warnings);

public sealed record AnalyticsWarningResponse(
    string Section,
    string ExceptionType,
    string Message);

public sealed record AnalyticsSectionErrorResponse(
    string Section,
    string ExceptionType,
    string Message);

public sealed record AnalyticsOverviewResponse(
    AnalyticsDateRange Range,
    IReadOnlyCollection<KeyMetricResponse> Metrics,
    TicketRevenueInstrumentationResponse TicketRevenueInstrumentation);

public sealed record KeyMetricResponse(
    string Key,
    string Label,
    decimal Value,
    string Unit,
    string HelpText);

public sealed record TicketRevenueInstrumentationResponse(
    bool IsInstrumented,
    bool StartedWithThisVersion,
    decimal PurchaseAmount,
    decimal DiscountAmount,
    string Note);

public sealed record AcquisitionAnalyticsResponse(
    AnalyticsDateRange Range,
    int TotalNewCustomers,
    int CustomersWithWelcomeTicket,
    int CustomersWithActivity,
    IReadOnlyCollection<AnalyticsBreakdownItemResponse> ByOrigin,
    IReadOnlyCollection<CustomerAcquisitionItemResponse> LatestCustomers);

public sealed record AnalyticsBreakdownItemResponse(string Key, string Label, decimal Value);

public sealed record CustomerAcquisitionItemResponse(
    Guid UserId,
    string DisplayName,
    string Origin,
    string OriginLabel,
    DateTime LinkedAtUtc,
    bool HasQrTicket);

public sealed record TicketsAnalyticsResponse(
    AnalyticsDateRange Range,
    int AssignedCount,
    int RedeemedCount,
    int ActiveCount,
    int ExpiredCount,
    decimal RedemptionRate,
    IReadOnlyCollection<AnalyticsBreakdownItemResponse> ByCategory,
    IReadOnlyCollection<AnalyticsBreakdownItemResponse> BySource,
    IReadOnlyCollection<TicketTemplateMetricResponse> TopTickets);

public sealed record TicketTemplateMetricResponse(
    Guid TicketTemplateId,
    string Nombre,
    string Tipo,
    string Categoria,
    int AssignedCount,
    int RedeemedCount,
    decimal RedemptionRate,
    decimal? Valor,
    int? PuntosCoste);

public sealed record MoneyAndPointsAnalyticsResponse(
    AnalyticsDateRange Range,
    decimal PointsTrackedRevenue,
    decimal TicketPurchaseAmount,
    decimal TicketDiscountAmount,
    decimal TicketFinalAmount,
    int PointsIssued,
    int PointsSpent,
    int LivePointsBalance,
    decimal AverageTrackedTicket,
    IReadOnlyCollection<CustomerValueMetricResponse> TopCustomers);

public sealed record CustomerValueMetricResponse(
    Guid UserId,
    decimal TrackedRevenue,
    int PointsEarned,
    int ActivityCount,
    DateTime LastActivityAtUtc);

public sealed record PromotionsAnalyticsResponse(
    AnalyticsDateRange Range,
    int CampaignCount,
    int AudienceCount,
    int PushEligibleCount,
    int PushDeliveredCount,
    int PushFailedCount,
    int TicketsGeneratedCount,
    int TicketsRedeemedCount,
    IReadOnlyCollection<CampaignMetricResponse> Campaigns);

public sealed record CampaignMetricResponse(
    Guid CampaignId,
    string TicketName,
    string Status,
    int AudienceCount,
    int PushEligibleCount,
    int PushDeliveredCount,
    int PushFailedCount,
    int TicketsGeneratedCount,
    int TicketsRedeemedCount,
    decimal RedemptionRate,
    DateTime CreatedAtUtc,
    DateTime StartsAtUtc,
    DateTime ExpiresAtUtc);

public sealed record OperationsAnalyticsResponse(
    AnalyticsDateRange Range,
    int CompletedPointOperations,
    int CancelledPointOperations,
    int FailedPinAttempts,
    int TicketValidationCount,
    IReadOnlyCollection<ValidatorMetricResponse> TopValidators);

public sealed record ValidatorMetricResponse(
    Guid ValidatorUserId,
    int TicketRedemptions,
    decimal TicketPurchaseAmount,
    DateTime LastActivityAtUtc);
