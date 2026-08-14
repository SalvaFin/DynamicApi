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
using Dynamic.Users.Domain.Enums;
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
        [FromQuery] BusinessAnalyticsQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (TryResolveRequest(request, out AnalyticsDateRange range, out IActionResult? rangeError) is false)
        {
            return rangeError!;
        }

        IActionResult? authorization = await AuthorizeAnalyticsAsync(negocioId, cancellationToken);
        if (authorization is not null)
        {
            return authorization;
        }

        AnalyticsAudienceFilter filter = await ResolveAudienceFilterAsync(request, cancellationToken);
        List<AnalyticsWarningResponse> warnings = [];

        return Ok(new BusinessAnalyticsResponse(
            range,
            await BuildSourceDataAsync(negocioId, range, filter, cancellationToken),
            await SafeBuildSectionAsync("overview", negocioId, range, filter, BuildOverviewAsync, EmptyOverview, warnings, cancellationToken),
            await SafeBuildSectionAsync("acquisition", negocioId, range, filter, BuildAcquisitionAsync, EmptyAcquisition, warnings, cancellationToken),
            await SafeBuildSectionAsync("tickets", negocioId, range, filter, BuildTicketsAsync, EmptyTickets, warnings, cancellationToken),
            await SafeBuildSectionAsync("moneyPoints", negocioId, range, filter, BuildMoneyAndPointsAsync, EmptyMoneyAndPoints, warnings, cancellationToken),
            await SafeBuildSectionAsync("promotions", negocioId, range, filter, BuildPromotionsAsync, EmptyPromotions, warnings, cancellationToken),
            await SafeBuildSectionAsync("operations", negocioId, range, filter, BuildOperationsAsync, EmptyOperations, warnings, cancellationToken),
            warnings));
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview(Guid negocioId, [FromQuery] BusinessAnalyticsQueryRequest request, CancellationToken cancellationToken = default)
        => await GetSectionAsync(negocioId, request, BuildOverviewAsync, cancellationToken);

    [HttpGet("acquisition")]
    public async Task<IActionResult> GetAcquisition(Guid negocioId, [FromQuery] BusinessAnalyticsQueryRequest request, CancellationToken cancellationToken = default)
        => await GetSectionAsync(negocioId, request, BuildAcquisitionAsync, cancellationToken);

    [HttpGet("tickets")]
    public async Task<IActionResult> GetTickets(Guid negocioId, [FromQuery] BusinessAnalyticsQueryRequest request, CancellationToken cancellationToken = default)
        => await GetSectionAsync(negocioId, request, BuildTicketsAsync, cancellationToken);

    [HttpGet("money-points")]
    public async Task<IActionResult> GetMoneyAndPoints(Guid negocioId, [FromQuery] BusinessAnalyticsQueryRequest request, CancellationToken cancellationToken = default)
        => await GetSectionAsync(negocioId, request, BuildMoneyAndPointsAsync, cancellationToken);

    [HttpGet("promotions")]
    public async Task<IActionResult> GetPromotions(Guid negocioId, [FromQuery] BusinessAnalyticsQueryRequest request, CancellationToken cancellationToken = default)
        => await GetSectionAsync(negocioId, request, BuildPromotionsAsync, cancellationToken);

    [HttpGet("operations")]
    public async Task<IActionResult> GetOperations(Guid negocioId, [FromQuery] BusinessAnalyticsQueryRequest request, CancellationToken cancellationToken = default)
        => await GetSectionAsync(negocioId, request, BuildOperationsAsync, cancellationToken);

    private async Task<IActionResult> GetSectionAsync<T>(
        Guid negocioId,
        BusinessAnalyticsQueryRequest request,
        Func<Guid, AnalyticsDateRange, AnalyticsAudienceFilter, CancellationToken, Task<T>> builder,
        CancellationToken cancellationToken)
    {
        if (TryResolveRequest(request, out AnalyticsDateRange range, out IActionResult? rangeError) is false)
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
            AnalyticsAudienceFilter filter = await ResolveAudienceFilterAsync(request, cancellationToken);
            return Ok(await builder(negocioId, range, filter, cancellationToken));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Error building analytics section for negocio {NegocioId}.", negocioId);
            return StatusCode(StatusCodes.Status500InternalServerError, new AnalyticsSectionErrorResponse(
                Section: typeof(T).Name,
                Message: "No se pudo calcular la seccion de metricas.",
                TraceId: HttpContext.TraceIdentifier));
        }
    }

    private async Task<T> SafeBuildSectionAsync<T>(
        string section,
        Guid negocioId,
        AnalyticsDateRange range,
        AnalyticsAudienceFilter filter,
        Func<Guid, AnalyticsDateRange, AnalyticsAudienceFilter, CancellationToken, Task<T>> builder,
        Func<AnalyticsDateRange, T> fallback,
        ICollection<AnalyticsWarningResponse> warnings,
        CancellationToken cancellationToken)
    {
        try
        {
            return await builder(negocioId, range, filter, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Error building analytics section {Section} for negocio {NegocioId}.", section, negocioId);
            warnings.Add(new AnalyticsWarningResponse(
                section,
                "No se pudo calcular temporalmente esta seccion."));
            return fallback(range);
        }
    }

    private async Task<AnalyticsOverviewResponse> BuildOverviewAsync(
        Guid negocioId,
        AnalyticsDateRange range,
        AnalyticsAudienceFilter filter,
        CancellationToken cancellationToken)
    {
        List<CustomerLinkProjection> customerLinks = await GetCustomerLinksAsync(negocioId, range, filter, cancellationToken);
        int newCustomers = customerLinks.Select(link => link.UserId).Distinct().Count();

        int activeCustomers = await GetActiveCustomerCountAsync(negocioId, range, filter, cancellationToken);
        int returningCustomers = await GetReturningCustomerCountAsync(negocioId, range, filter, cancellationToken);

        decimal pointsTrackedRevenue = await PointsTransactions(negocioId, range, filter)
            .Where(transaction =>
                transaction.TransactionType == PointsTransactionType.Earn ||
                transaction.TransactionType == PointsTransactionType.BackofficeEarn)
            .SumAsync(transaction => transaction.AmountEuros ?? 0m, cancellationToken);

        int pointsIssued = await PointsTransactions(negocioId, range, filter)
            .Where(transaction =>
                transaction.TransactionType == PointsTransactionType.Earn ||
                transaction.TransactionType == PointsTransactionType.BackofficeEarn ||
                transaction.TransactionType == PointsTransactionType.TransferIn)
            .SumAsync(transaction => transaction.PointsAmount, cancellationToken);

        List<TicketRedemption> ticketRedemptions = await GetTicketRedemptionsAsync(negocioId, range, filter, cancellationToken);
        decimal ticketPurchaseAmount = ticketRedemptions.Sum(redemption => redemption.PurchaseAmount ?? 0m);
        decimal ticketDiscountAmount = ticketRedemptions.Sum(redemption => redemption.DiscountAmount ?? 0m);

        int assignedTickets = await AssignedTickets(negocioId, filter)
            .Where(ticket => ticket.CreatedAtUtc >= range.FromUtc && ticket.CreatedAtUtc < range.ToUtc)
            .CountAsync(cancellationToken);

        int redeemedTickets = await CountTicketRedemptionsIncludingLegacyAsync(negocioId, range, filter, ticketRedemptions, cancellationToken);
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
        AnalyticsAudienceFilter filter,
        CancellationToken cancellationToken)
    {
        List<CustomerLinkProjection> links = (await GetCustomerLinksAsync(negocioId, range, filter, cancellationToken))
            .OrderByDescending(link => link.CreatedAtUtc)
            .ToList();

        IQueryable<PendingTicketAssignment> qrAssignmentsQuery = _fidelityDbContext.PendingTicketAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.NegocioId == negocioId &&
                assignment.Activated &&
                assignment.ActivatedAtUtc >= range.FromUtc &&
                assignment.ActivatedAtUtc < range.ToUtc);

        if (filter.UserIds is not null)
        {
            qrAssignmentsQuery = qrAssignmentsQuery.Where(assignment => filter.UserIds.Contains(assignment.UserId));
        }

        List<PendingTicketAssignment> qrAssignments = await qrAssignmentsQuery.ToListAsync(cancellationToken);

        List<AnalyticsBreakdownItemResponse> byOrigin = links
            .GroupBy(link => NormalizeOrigin(link.Origin))
            .Select(group => new AnalyticsBreakdownItemResponse(group.Key, ResolveOriginLabel(group.Key), group.Select(link => link.UserId).Distinct().Count()))
            .OrderByDescending(item => item.Value)
            .ToList();

        if (qrAssignments.Count > 0)
        {
            byOrigin.Add(new AnalyticsBreakdownItemResponse("qr_ticket", "QR de bienvenida", qrAssignments.Select(assignment => assignment.UserId).Distinct().Count()));
        }

        int customersWithFirstTicket = await AssignedTickets(negocioId, filter)
            .Where(ticket =>
                ticket.CategoriaEnvioEspecial == CategoriaEnvioTicket.PrimerRegistro &&
                ticket.CreatedAtUtc >= range.FromUtc &&
                ticket.CreatedAtUtc < range.ToUtc)
            .Select(ticket => ticket.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        int customersWithActivity = await GetActiveCustomerCountAsync(negocioId, range, filter, cancellationToken);

        return new AcquisitionAnalyticsResponse(
            range,
            TotalNewCustomers: links.Select(link => link.UserId).Distinct().Count(),
            CustomersWithWelcomeTicket: customersWithFirstTicket,
            CustomersWithActivity: customersWithActivity,
            ByOrigin: byOrigin.OrderByDescending(item => item.Value).ToArray());
    }

    private static AcquisitionAnalyticsResponse EmptyAcquisition(AnalyticsDateRange range)
        => new(range, 0, 0, 0, []);

    private async Task<TicketsAnalyticsResponse> BuildTicketsAsync(
        Guid negocioId,
        AnalyticsDateRange range,
        AnalyticsAudienceFilter filter,
        CancellationToken cancellationToken)
    {
        int assigned = await AssignedTickets(negocioId, filter)
            .Where(ticket => ticket.CreatedAtUtc >= range.FromUtc && ticket.CreatedAtUtc < range.ToUtc)
            .CountAsync(cancellationToken);

        List<TicketRedemption> redemptions = await GetTicketRedemptionsAsync(negocioId, range, filter, cancellationToken);
        int redeemed = await CountTicketRedemptionsIncludingLegacyAsync(negocioId, range, filter, redemptions, cancellationToken);

        int active = await AssignedTickets(negocioId, filter)
            .Where(ticket => ticket.Activo && !ticket.Usado && ticket.ExpiresAtUtc > range.ToUtc)
            .CountAsync(cancellationToken);

        int expired = await AssignedTickets(negocioId, filter)
            .Where(ticket => !ticket.Usado && ticket.ExpiresAtUtc >= range.FromUtc && ticket.ExpiresAtUtc < range.ToUtc)
            .CountAsync(cancellationToken);

        List<Ticket> assignedTickets = await AssignedTickets(negocioId, filter)
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
        AnalyticsAudienceFilter filter,
        CancellationToken cancellationToken)
    {
        List<PointsTransaction> transactions = await PointsTransactions(negocioId, range, filter)
            .ToListAsync(cancellationToken);

        List<TicketRedemption> redemptions = await GetTicketRedemptionsAsync(negocioId, range, filter, cancellationToken);

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

        IQueryable<Dynamic.Fidelity.Domain.Entities.Points> livePointsQuery = _fidelityDbContext.Points
            .AsNoTracking()
            .Where(points => points.NegocioId == negocioId);

        if (filter.UserIds is not null)
        {
            livePointsQuery = livePointsQuery.Where(points => filter.UserIds.Contains(points.UserId));
        }

        int livePointsBalance = await livePointsQuery.SumAsync(points => points.CurrentBalance, cancellationToken);

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
                : decimal.Round(pointsTrackedRevenue / transactions.Count(transaction => transaction.AmountEuros.HasValue), 2));
    }

    private static MoneyAndPointsAnalyticsResponse EmptyMoneyAndPoints(AnalyticsDateRange range)
        => new(range, 0, 0, 0, 0, 0, 0, 0, 0);

    private async Task<PromotionsAnalyticsResponse> BuildPromotionsAsync(
        Guid negocioId,
        AnalyticsDateRange range,
        AnalyticsAudienceFilter filter,
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

        List<Guid> campaignIds = campaigns.Select(campaign => campaign.Id).ToList();

        Dictionary<Guid, int> filteredAudienceCounts = [];
        Dictionary<Guid, int> filteredPushEligibleCounts = [];
        Dictionary<Guid, int> filteredPushDeliveredCounts = [];
        Dictionary<Guid, int> filteredPushFailedCounts = [];

        if (filter.UserIds is not null && campaignIds.Count > 0)
        {
            var recipientRows = await _promotionsDbContext.Recipients
                .AsNoTracking()
                .Where(recipient =>
                    campaignIds.Contains(recipient.CampaignId) &&
                    filter.UserIds.Contains(recipient.UserId))
                .Select(recipient => new { recipient.CampaignId, recipient.Id })
                .ToListAsync(cancellationToken);

            filteredAudienceCounts = recipientRows
                .GroupBy(recipient => recipient.CampaignId)
                .ToDictionary(group => group.Key, group => group.Count());

            var deliveryRows = await _promotionsDbContext.Deliveries
                .AsNoTracking()
                .Where(delivery =>
                    campaignIds.Contains(delivery.CampaignId) &&
                    filter.UserIds.Contains(delivery.UserId))
                .Select(delivery => new { delivery.CampaignId, delivery.RecipientId, delivery.Status })
                .ToListAsync(cancellationToken);

            filteredPushEligibleCounts = deliveryRows
                .GroupBy(delivery => delivery.CampaignId)
                .ToDictionary(group => group.Key, group => group.Select(delivery => delivery.RecipientId).Distinct().Count());
            filteredPushDeliveredCounts = deliveryRows
                .Where(delivery => delivery.Status == PromotionDeliveryStatus.Delivered)
                .GroupBy(delivery => delivery.CampaignId)
                .ToDictionary(group => group.Key, group => group.Count());
            filteredPushFailedCounts = deliveryRows
                .Where(delivery => delivery.Status == PromotionDeliveryStatus.Failed)
                .GroupBy(delivery => delivery.CampaignId)
                .ToDictionary(group => group.Key, group => group.Count());
        }

        List<Ticket> campaignTickets = await AssignedTickets(negocioId, filter)
            .Where(ticket => ticket.SourcePromotionCampaignId.HasValue && campaignIds.Contains(ticket.SourcePromotionCampaignId ?? Guid.Empty))
            .ToListAsync(cancellationToken);

        List<TicketRedemption> campaignRedemptions = (await GetTicketRedemptionsAsync(
                negocioId,
                range,
                filter,
                cancellationToken))
            .Where(redemption =>
                redemption.SourcePromotionCampaignId.HasValue &&
                campaignIds.Contains(redemption.SourcePromotionCampaignId ?? Guid.Empty))
            .ToList();

        CampaignMetricResponse[] campaignMetrics = campaigns
            .Select(campaign =>
            {
                int generatedTickets = campaignTickets.Count(ticket => ticket.SourcePromotionCampaignId == campaign.Id);
                int redeemedTickets = campaignRedemptions.Count(redemption => redemption.SourcePromotionCampaignId == campaign.Id);
                int audienceCount = filter.UserIds is null
                    ? campaign.AudienceCount
                    : filteredAudienceCounts.GetValueOrDefault(campaign.Id);
                int pushEligibleCount = filter.UserIds is null
                    ? campaign.PushEligibleCount
                    : filteredPushEligibleCounts.GetValueOrDefault(campaign.Id);
                int pushDeliveredCount = filter.UserIds is null
                    ? campaign.PushDeliveredCount
                    : filteredPushDeliveredCounts.GetValueOrDefault(campaign.Id);
                int pushFailedCount = filter.UserIds is null
                    ? campaign.PushFailedCount
                    : filteredPushFailedCounts.GetValueOrDefault(campaign.Id);
                return new CampaignMetricResponse(
                    campaign.Id,
                    campaign.TicketNombreSnapshot,
                    campaign.Status.ToString(),
                    audienceCount,
                    pushEligibleCount,
                    pushDeliveredCount,
                    pushFailedCount,
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
            AudienceCount: campaignMetrics.Sum(campaign => campaign.AudienceCount),
            PushEligibleCount: campaignMetrics.Sum(campaign => campaign.PushEligibleCount),
            PushDeliveredCount: campaignMetrics.Sum(campaign => campaign.PushDeliveredCount),
            PushFailedCount: campaignMetrics.Sum(campaign => campaign.PushFailedCount),
            TicketsGeneratedCount: campaignTickets.Count,
            TicketsRedeemedCount: campaignRedemptions.Count,
            Campaigns: campaignMetrics);
    }

    private static PromotionsAnalyticsResponse EmptyPromotions(AnalyticsDateRange range)
        => new(range, 0, 0, 0, 0, 0, 0, 0, []);

    private async Task<OperationsAnalyticsResponse> BuildOperationsAsync(
        Guid negocioId,
        AnalyticsDateRange range,
        AnalyticsAudienceFilter filter,
        CancellationToken cancellationToken)
    {
        IQueryable<Dynamic.Fidelity.Domain.Entities.PointsOperation> pointOperations = _fidelityDbContext.PointsOperations
            .AsNoTracking()
            .Where(operation => operation.NegocioId == negocioId);
        IQueryable<Dynamic.Fidelity.Domain.Entities.PointsOperationAttempt> pointAttempts = _fidelityDbContext.PointsOperationAttempts
            .AsNoTracking()
            .Where(attempt => attempt.NegocioId == negocioId);

        if (filter.UserIds is not null)
        {
            pointOperations = pointOperations.Where(operation => filter.UserIds.Contains(operation.UserId));
            pointAttempts = pointAttempts.Where(attempt => filter.UserIds.Contains(attempt.UserId));
        }

        int completedPointOperations = await pointOperations
            .Where(operation =>
                operation.Status == PointsOperationStatus.Completed &&
                operation.ValidatedAtUtc >= range.FromUtc &&
                operation.ValidatedAtUtc < range.ToUtc)
            .CountAsync(cancellationToken);

        int cancelledPointOperations = await pointOperations
            .Where(operation =>
                operation.Status == PointsOperationStatus.Cancelled &&
                operation.CancelledAtUtc >= range.FromUtc &&
                operation.CancelledAtUtc < range.ToUtc)
            .CountAsync(cancellationToken);

        int failedPinAttempts = await pointAttempts
            .Where(attempt =>
                !attempt.Succeeded &&
                attempt.CreatedAtUtc >= range.FromUtc &&
                attempt.CreatedAtUtc < range.ToUtc)
            .CountAsync(cancellationToken);

        List<TicketRedemption> redemptions = await GetTicketRedemptionsAsync(negocioId, range, filter, cancellationToken);

        return new OperationsAnalyticsResponse(
            range,
            CompletedPointOperations: completedPointOperations,
            CancelledPointOperations: cancelledPointOperations,
            FailedPinAttempts: failedPinAttempts,
            TicketValidationCount: redemptions.Count);
    }

    private static OperationsAnalyticsResponse EmptyOperations(AnalyticsDateRange range)
        => new(range, 0, 0, 0, 0);

    private async Task<AnalyticsSourceDataResponse> BuildSourceDataAsync(
        Guid negocioId,
        AnalyticsDateRange range,
        AnalyticsAudienceFilter filter,
        CancellationToken cancellationToken)
    {
        int audienceMembershipsInRange = await SafeCountAsync(async () =>
            await _negociosDbContext.NegociosAudiencias
                .AsNoTracking()
                .Where(link =>
                    link.NegocioId == negocioId &&
                    link.FechaAltaUtc >= range.FromUtc &&
                    link.FechaAltaUtc < range.ToUtc &&
                    (filter.UserIds == null || filter.UserIds.Contains(link.UserId)))
                .CountAsync(cancellationToken));

        int legacyCustomerLinksInRange = await SafeCountAsync(async () =>
            await _negociosDbContext.NegociosUsuariosVinculaciones
                .AsNoTracking()
                .Where(link =>
                    link.NegocioId == negocioId &&
                    link.TipoVinculacion == TipoVinculacionNegocioUsuario.Cliente &&
                    (link.FechaAceptacionUtc ?? link.FechaInicioUtc ?? link.CreatedAtUtc) >= range.FromUtc &&
                    (link.FechaAceptacionUtc ?? link.FechaInicioUtc ?? link.CreatedAtUtc) < range.ToUtc &&
                    (filter.UserIds == null || filter.UserIds.Contains(link.UserId)))
                .CountAsync(cancellationToken));

        int assignedTicketsInRange = await SafeCountAsync(async () =>
            await AssignedTickets(negocioId, filter)
                .Where(ticket => ticket.CreatedAtUtc >= range.FromUtc && ticket.CreatedAtUtc < range.ToUtc)
                .CountAsync(cancellationToken));

        int redeemedTicketsInRange = await SafeCountAsync(async () =>
            await AssignedTickets(negocioId, filter)
                .Where(ticket => ticket.UsedAtUtc >= range.FromUtc && ticket.UsedAtUtc < range.ToUtc)
                .CountAsync(cancellationToken));

        int ticketRedemptionsInRange = await SafeCountAsync(async () =>
            await _fidelityDbContext.TicketRedemptions
                .AsNoTracking()
                .Where(redemption =>
                    redemption.NegocioId == negocioId &&
                    redemption.CreatedAtUtc >= range.FromUtc &&
                    redemption.CreatedAtUtc < range.ToUtc &&
                    (filter.UserIds == null || filter.UserIds.Contains(redemption.UserId)))
                .CountAsync(cancellationToken));

        int pointsTransactionsInRange = await SafeCountAsync(async () =>
            await PointsTransactions(negocioId, range, filter)
                .CountAsync(cancellationToken));

        int promotionCampaignsInRange = await SafeCountAsync(async () =>
            await _promotionsDbContext.Campaigns
                .AsNoTracking()
                .Where(campaign =>
                    campaign.NegocioId == negocioId &&
                    campaign.CreatedAtUtc >= range.FromUtc &&
                    campaign.CreatedAtUtc < range.ToUtc)
                .CountAsync(cancellationToken));

        return new AnalyticsSourceDataResponse(
            AudienceMembershipsInRange: audienceMembershipsInRange,
            LegacyCustomerLinksInRange: legacyCustomerLinksInRange,
            AssignedTicketsInRange: assignedTicketsInRange,
            RedeemedTicketsInRange: redeemedTicketsInRange,
            TicketRedemptionsInRange: ticketRedemptionsInRange,
            PointsTransactionsInRange: pointsTransactionsInRange,
            PromotionCampaignsInRange: promotionCampaignsInRange);
    }

    private async Task<int> SafeCountAsync(Func<Task<int>> count)
    {
        try
        {
            return await count();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Could not calculate analytics source data count.");
            return 0;
        }
    }

    private async Task<List<CustomerLinkProjection>> GetCustomerLinksAsync(
        Guid negocioId,
        AnalyticsDateRange range,
        AnalyticsAudienceFilter filter,
        CancellationToken cancellationToken)
    {
        try
        {
            IQueryable<NegocioAudiencia> query = _negociosDbContext.NegociosAudiencias
                .AsNoTracking()
                .Where(link =>
                    link.NegocioId == negocioId &&
                    link.FechaAltaUtc >= range.FromUtc &&
                    link.FechaAltaUtc < range.ToUtc);

            if (filter.UserIds is not null)
            {
                query = query.Where(link => filter.UserIds.Contains(link.UserId));
            }

            return await query
                .Select(link => new CustomerLinkProjection(
                    link.UserId,
                    link.OrigenAlta,
                    link.FechaAltaUtc))
                .ToListAsync(cancellationToken);
        }
        catch (DbException)
        {
            IQueryable<NegocioUsuarioVinculacion> query = _negociosDbContext.NegociosUsuariosVinculaciones
                .AsNoTracking()
                .Where(link =>
                    link.NegocioId == negocioId &&
                    link.TipoVinculacion == TipoVinculacionNegocioUsuario.Cliente &&
                    (link.FechaAceptacionUtc ?? link.FechaInicioUtc ?? link.CreatedAtUtc) >= range.FromUtc &&
                    (link.FechaAceptacionUtc ?? link.FechaInicioUtc ?? link.CreatedAtUtc) < range.ToUtc);

            if (filter.UserIds is not null)
            {
                query = query.Where(link => filter.UserIds.Contains(link.UserId));
            }

            return await query
                .Select(link => new CustomerLinkProjection(
                    link.UserId,
                    link.OrigenVinculacion,
                    link.FechaAceptacionUtc ?? link.FechaInicioUtc ?? link.CreatedAtUtc))
                .ToListAsync(cancellationToken);
        }
    }

    private IQueryable<Ticket> AssignedTickets(Guid negocioId, AnalyticsAudienceFilter filter)
    {
        IQueryable<Ticket> query = _fidelityDbContext.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.NegocioId == negocioId && !ticket.EsPlantilla && ticket.UserId.HasValue);

        return filter.UserIds is null
            ? query
            : query.Where(ticket => filter.UserIds.Contains(ticket.UserId!.Value));
    }

    private async Task<List<TicketRedemption>> GetTicketRedemptionsAsync(
        Guid negocioId,
        AnalyticsDateRange range,
        AnalyticsAudienceFilter filter,
        CancellationToken cancellationToken)
    {
        try
        {
            IQueryable<TicketRedemption> query = _fidelityDbContext.TicketRedemptions
                .AsNoTracking()
                .Where(redemption =>
                    redemption.NegocioId == negocioId &&
                    redemption.CreatedAtUtc >= range.FromUtc &&
                    redemption.CreatedAtUtc < range.ToUtc);

            if (filter.UserIds is not null)
            {
                query = query.Where(redemption => filter.UserIds.Contains(redemption.UserId));
            }

            return await query
                .ToListAsync(cancellationToken);
        }
        catch (DbException)
        {
            return [];
        }
    }

    private IQueryable<PointsTransaction> PointsTransactions(
        Guid negocioId,
        AnalyticsDateRange range,
        AnalyticsAudienceFilter filter)
    {
        IQueryable<PointsTransaction> query = _fidelityDbContext.PointsTransactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.NegocioId == negocioId &&
                transaction.CreatedAtUtc >= range.FromUtc &&
                transaction.CreatedAtUtc < range.ToUtc);

        return filter.UserIds is null
            ? query
            : query.Where(transaction => filter.UserIds.Contains(transaction.UserId));
    }

    private async Task<int> CountTicketRedemptionsIncludingLegacyAsync(
        Guid negocioId,
        AnalyticsDateRange range,
        AnalyticsAudienceFilter filter,
        IReadOnlyCollection<TicketRedemption> instrumentedRedemptions,
        CancellationToken cancellationToken)
    {
        List<Guid> instrumentedTicketIds = instrumentedRedemptions
            .Select(redemption => redemption.TicketId)
            .Distinct()
            .ToList();

        int legacyRedemptions = await AssignedTickets(negocioId, filter)
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
        AnalyticsAudienceFilter filter,
        CancellationToken cancellationToken)
    {
        Guid[] pointsUsers = await PointsTransactions(negocioId, range, filter)
            .Select(transaction => transaction.UserId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        Guid[] ticketUsers = (await GetTicketRedemptionsAsync(negocioId, range, filter, cancellationToken))
            .Select(redemption => redemption.UserId)
            .Distinct()
            .ToArray();

        Guid[] legacyTicketUsers = await AssignedTickets(negocioId, filter)
            .Where(ticket =>
                (ticket.CreatedAtUtc >= range.FromUtc && ticket.CreatedAtUtc < range.ToUtc) ||
                (ticket.UpdatedAtUtc >= range.FromUtc && ticket.UpdatedAtUtc < range.ToUtc) ||
                (ticket.UsedAtUtc >= range.FromUtc && ticket.UsedAtUtc < range.ToUtc))
            .Select(ticket => ticket.UserId ?? Guid.Empty)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        Guid[] linkedUsers = (await GetCustomerLinksAsync(negocioId, range, filter, cancellationToken))
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
        AnalyticsAudienceFilter filter,
        CancellationToken cancellationToken)
    {
        Guid[] recurringPointUsers = await PointsTransactions(negocioId, range, filter)
            .GroupBy(transaction => transaction.UserId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArrayAsync(cancellationToken);

        Guid[] recurringTicketUsers = (await GetTicketRedemptionsAsync(negocioId, range, filter, cancellationToken))
            .GroupBy(redemption => redemption.UserId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        Guid[] recurringLegacyTicketUsers = await AssignedTickets(negocioId, filter)
            .Where(ticket =>
                (ticket.CreatedAtUtc >= range.FromUtc && ticket.CreatedAtUtc < range.ToUtc) ||
                (ticket.UpdatedAtUtc >= range.FromUtc && ticket.UpdatedAtUtc < range.ToUtc) ||
                (ticket.UsedAtUtc >= range.FromUtc && ticket.UsedAtUtc < range.ToUtc))
            .GroupBy(ticket => ticket.UserId ?? Guid.Empty)
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

        Guid requesterId = userId.Value;
        BusinessLinkProjection? link = await _negociosDbContext.NegociosUsuariosVinculaciones
            .AsNoTracking()
            .Where(item => item.NegocioId == negocioId && item.UserId == requesterId)
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

    private bool TryResolveRequest(
        BusinessAnalyticsQueryRequest request,
        out AnalyticsDateRange range,
        out IActionResult? error)
    {
        if (request.MinimumAge is < 0 or > 130 || request.MaximumAge is < 0 or > 130)
        {
            range = new AnalyticsDateRange(default, default);
            error = BadRequest(new { message = "Las edades deben estar entre 0 y 130." });
            return false;
        }

        if (request.MinimumAge.HasValue && request.MaximumAge.HasValue && request.MinimumAge > request.MaximumAge)
        {
            range = new AnalyticsDateRange(default, default);
            error = BadRequest(new { message = "La edad minima no puede ser mayor que la edad maxima." });
            return false;
        }

        return TryResolveRange(request.FromUtc, request.ToUtc, out range, out error);
    }

    private async Task<AnalyticsAudienceFilter> ResolveAudienceFilterAsync(
        BusinessAnalyticsQueryRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.Gender.HasValue && !request.MinimumAge.HasValue && !request.MaximumAge.HasValue)
        {
            return new AnalyticsAudienceFilter(null);
        }

        IQueryable<UserAccount> query = _usersDbContext.Users.AsNoTracking();

        if (request.Gender.HasValue)
        {
            UserGender gender = request.Gender.Value;
            query = query.Where(user => user.Gender == gender);
        }

        DateTime todayUtc = DateTime.UtcNow.Date;
        if (request.MinimumAge.HasValue)
        {
            DateTime latestBirthDate = todayUtc.AddYears(-request.MinimumAge.Value);
            query = query.Where(user => user.BirthDate.HasValue && user.BirthDate.Value <= latestBirthDate);
        }

        if (request.MaximumAge.HasValue)
        {
            DateTime earliestBirthDateExclusive = todayUtc.AddYears(-(request.MaximumAge.Value + 1));
            query = query.Where(user => user.BirthDate.HasValue && user.BirthDate.Value > earliestBirthDateExclusive);
        }

        HashSet<Guid> userIds = (await query
            .Select(user => user.Id)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        return new AnalyticsAudienceFilter(userIds);
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

}

public sealed record AnalyticsDateRange(DateTime FromUtc, DateTime ToUtc);

public sealed class BusinessAnalyticsQueryRequest
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public UserGender? Gender { get; set; }
    public int? MinimumAge { get; set; }
    public int? MaximumAge { get; set; }
}

internal sealed record AnalyticsAudienceFilter(IReadOnlySet<Guid>? UserIds);

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
    AnalyticsSourceDataResponse SourceData,
    AnalyticsOverviewResponse Overview,
    AcquisitionAnalyticsResponse Acquisition,
    TicketsAnalyticsResponse Tickets,
    MoneyAndPointsAnalyticsResponse MoneyAndPoints,
    PromotionsAnalyticsResponse Promotions,
    OperationsAnalyticsResponse Operations,
    IReadOnlyCollection<AnalyticsWarningResponse> Warnings);

public sealed record AnalyticsSourceDataResponse(
    int AudienceMembershipsInRange,
    int LegacyCustomerLinksInRange,
    int AssignedTicketsInRange,
    int RedeemedTicketsInRange,
    int TicketRedemptionsInRange,
    int PointsTransactionsInRange,
    int PromotionCampaignsInRange);

public sealed record AnalyticsWarningResponse(
    string Section,
    string Message);

public sealed record AnalyticsSectionErrorResponse(
    string Section,
    string Message,
    string? TraceId = null);

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
    IReadOnlyCollection<AnalyticsBreakdownItemResponse> ByOrigin);

public sealed record AnalyticsBreakdownItemResponse(string Key, string Label, decimal Value);

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
    decimal AverageTrackedTicket);

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
    int TicketValidationCount);
