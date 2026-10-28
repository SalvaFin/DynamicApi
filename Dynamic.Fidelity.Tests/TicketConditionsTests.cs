using System.Text.Json;
using Dynamic.Fidelity.Application.DTOs.Requests;
using Dynamic.Fidelity.Application.Options;
using Dynamic.Fidelity.Application.Services;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Dynamic.Fidelity.Infrastructure.Repositories;
using Dynamic.Negocios.Domain.Entities;
using Dynamic.Negocios.Domain.Enums;
using Dynamic.Negocios.Infrastructure.Persistence;
using Dynamic.Negocios.Infrastructure.Repositories;
using Dynamic.Notify.Application.Contracts;
using Dynamic.Notify.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Dynamic.Fidelity.Tests;

public class TicketConditionsTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("0", false)]
    [InlineData("19.99", false)]
    [InlineData("20", true)]
    [InlineData("21", true)]
    public async Task BothQrAndCodeEnforceMinimumWithoutConsumingRejectedTickets(string? amount, bool succeeds)
    {
        foreach (bool useQr in new[] { false, true })
        {
            await using var f = await Fixture.Create();
            f.Ticket.GastoMinimoRequerido = 20m;
            await f.Db.SaveChangesAsync();
            var qr = await f.Service.GenerateAssignedTicketQrAsync(f.Ticket.Id, f.Ticket.UserId!.Value);
            Assert.True(qr.Succeeded);
            var result = await f.Service.ValidateAssignedTicketQrAsync(f.Business.Id, f.EmployeeId, false, new ValidateTicketQrRequest
            {
                QrToken = useQr ? qr.Data!.QrToken : null,
                TicketCode = useQr ? null : f.Ticket.CodigoVisible,
                PurchaseAmount = amount is null ? null : decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)
            });
            Assert.Equal(succeeds, result.Succeeded);
            f.Db.ChangeTracker.Clear();
            var saved = await f.Db.Tickets.SingleAsync();
            Assert.Equal(succeeds ? 1 : 0, saved.UsosConsumidos);
            Assert.Equal(succeeds, saved.Usado);
            Assert.Equal(succeeds ? 1 : 0, await f.Db.TicketRedemptions.CountAsync());
            var appEvent = Assert.Single(f.Events.Items);
            Assert.Equal(f.Ticket.UserId, appEvent.UserId);
            Assert.Equal(succeeds ? "fidelity.ticket.used" : "fidelity.ticket.rejected", appEvent.Event.Type);
            if (!succeeds)
            {
                Assert.Contains("cesta mínima", result.ErrorMessage);
                var payload = JsonSerializer.SerializeToElement(appEvent.Event.Payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                Assert.Equal(result.ErrorMessage, payload.GetProperty("message").GetString());
                Assert.Equal(f.Ticket.Id.ToString(), payload.GetProperty("ticketId").GetString());
                Assert.Null(saved.UsedAtUtc);
            }
        }
    }

    [Fact]
    public async Task UnrestrictedTicketStillValidatesWithoutAmount()
    {
        await using var f = await Fixture.Create();
        var result = await f.Service.ValidateAssignedTicketQrAsync(f.Business.Id, f.EmployeeId, false,
            new ValidateTicketQrRequest { TicketCode = f.Ticket.CodigoVisible });
        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Null(result.Data!.PurchaseAmount);
        Assert.Single(f.Events.Items, x => x.Event.Type == "fidelity.ticket.used");
    }

    [Fact]
    public async Task OutsideAllowedDayNotifiesCustomerAndPreservesTicket()
    {
        await using var f = await Fixture.Create();
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid"));
        int today = local.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)local.DayOfWeek;
        f.Ticket.UsageWindows = [new() { DayOfWeek = today == 7 ? 1 : today + 1 }];
        await f.Db.SaveChangesAsync();
        var result = await f.Service.ValidateAssignedTicketQrAsync(f.Business.Id, f.EmployeeId, false,
            new ValidateTicketQrRequest { TicketCode = f.Ticket.CodigoVisible });
        Assert.False(result.Succeeded);
        Assert.Contains("solo es válido", result.ErrorMessage);
        Assert.False(f.Ticket.Usado);
        Assert.Empty(f.Db.TicketRedemptions);
        Assert.Single(f.Events.Items, x => x.Event.Type == "fidelity.ticket.rejected");
    }

    [Theory]
    [InlineData("2026-07-06T13:59:00Z", false)] // Monday 15:59 Madrid
    [InlineData("2026-07-06T14:00:00Z", true)]
    [InlineData("2026-07-06T16:59:59Z", true)]
    [InlineData("2026-07-06T17:00:00Z", false)]
    [InlineData("2026-07-07T14:00:00Z", false)]
    [InlineData("2026-01-05T15:00:00Z", true)] // winter UTC+1
    public void ScheduleUsesLocalWeekdayDstAndExclusiveEnd(string utc, bool allowed)
    {
        var ticket = new Ticket { UsageWindows = [new() { DayOfWeek = 1, StartTime = "16:00", EndTime = "19:00" }] };
        var result = TicketUsagePolicy.Check(ticket, null, DateTime.Parse(utc).ToUniversalTime(), "Europe/Madrid");
        Assert.Equal(allowed, result.Succeeded);
        if (!allowed) Assert.Contains("los lunes de 16:00 a 19:00", result.ErrorMessage);
    }

    [Theory]
    [InlineData("2026-07-12T21:59:00Z", false)] // Sunday 23:59: window starts Monday
    [InlineData("2026-07-13T20:00:00Z", true)] // Monday 22
    [InlineData("2026-07-14T00:59:00Z", true)] // Tuesday 02:59
    [InlineData("2026-07-14T01:00:00Z", false)]
    public void OvernightWindowBelongsToItsStartingDay(string utc, bool allowed)
    {
        var ticket = new Ticket { UsageWindows = [new() { DayOfWeek = 1, StartTime = "22:00", EndTime = "03:00" }] };
        Assert.Equal(allowed, TicketUsagePolicy.Check(ticket, null, DateTime.Parse(utc).ToUniversalTime(), null).Succeeded);
    }

    [Fact]
    public void FullDaysMultipleWindowsAndDifferentLocalTimeZone()
    {
        var ticket = new Ticket { UsageWindows = [new() { DayOfWeek = 7 }, new() { DayOfWeek = 1, StartTime = "09:00", EndTime = "10:00" }] };
        var mondayUtc = new DateTime(2026, 7, 6, 0, 30, 0, DateTimeKind.Utc);
        Assert.True(TicketUsagePolicy.Check(ticket, null, mondayUtc, "America/New_York").Succeeded); // Sunday locally
        Assert.False(TicketUsagePolicy.Check(ticket, null, mondayUtc, "Europe/Madrid").Succeeded);
        Assert.False(TicketUsagePolicy.Check(ticket, null, mondayUtc, "invalid-zone").Succeeded);
        Assert.True(TicketUsagePolicy.Check(ticket, null, new DateTime(2026, 7, 6, 7, 30, 0, DateTimeKind.Utc), null).Succeeded);
    }

    [Theory]
    [InlineData(0, null, null)]
    [InlineData(8, null, null)]
    [InlineData(1, "16:00", null)]
    [InlineData(1, "16:00", "16:00")]
    [InlineData(1, "24:00", "18:00")]
    [InlineData(1, "9:00", "18:00")]
    public void RejectsMalformedSchedule(int day, string? start, string? end)
        => Assert.False(TicketUsagePolicy.ValidateConfiguration(null, [new() { DayOfWeek = day, StartTime = start, EndTime = end }]).Succeeded);

    [Fact]
    public async Task SettingsIncludeAssignedRestrictedTicketsAndRespectStaffAuthorization()
    {
        await using var f = await Fixture.Create();
        var before = await f.Service.RequiresPurchaseAmountAsync(f.Business.Id, f.EmployeeId, false);
        Assert.True(before.Succeeded); Assert.False(before.Data);
        f.Ticket.GastoMinimoRequerido = 20;
        await f.Db.SaveChangesAsync();
        Assert.True((await f.Service.RequiresPurchaseAmountAsync(f.Business.Id, f.EmployeeId, false)).Data);
        var forbidden = await f.Service.RequiresPurchaseAmountAsync(f.Business.Id, Guid.NewGuid(), false);
        Assert.Equal("forbidden", forbidden.ErrorCode);
        var other = new Negocio { Id = Guid.NewGuid(), NombreComercial = "Otro" };
        f.BusinessDb.Add(other); await f.BusinessDb.SaveChangesAsync();
        Assert.False((await f.Service.RequiresPurchaseAmountAsync(other.Id, Guid.NewGuid(), true)).Data);
        var wrongBusiness = await f.Service.ValidateAssignedTicketQrAsync(other.Id, Guid.NewGuid(), true, new() { TicketCode = f.Ticket.CodigoVisible });
        Assert.False(wrongBusiness.Succeeded); Assert.Empty(f.Events.Items);
    }

    [Fact]
    public async Task CreateUpdatePersistConditionsAndRejectInvalidMinimum()
    {
        await using var f = await Fixture.Create();
        var service = new TicketService(f.Db, f.BusinessDb, new TicketRepository(f.Db), null!, new NegocioRepository(f.BusinessDb), null!, null!, null!);
        var result = await service.CreateAsync(f.Business.Id, f.EmployeeId, true, new CreateTicketRequest
        {
            Nombre = "Valle", GastoMinimoRequerido = 20, UsageWindows = [new() { DayOfWeek = 1, StartTime = "16:00", EndTime = "19:00" }]
        });
        Assert.True(result.Succeeded, result.ErrorMessage);
        f.Db.ChangeTracker.Clear();
        var saved = await f.Db.Tickets.SingleAsync(x => x.Id == result.Data!.Id);
        Assert.Equal(20, saved.GastoMinimoRequerido);
        Assert.Single(saved.UsageWindows!);
        Assert.Contains("16:00", result.Data!.CondicionesValidacion);
        Assert.False((await service.UpdateAsync(f.Business.Id, saved.Id, f.EmployeeId, true, new UpdateTicketRequest { Nombre = "Mal", GastoMinimoRequerido = -1 })).Succeeded);
        Assert.False((await service.CreateAsync(f.Business.Id, f.EmployeeId, true, new CreateTicketRequest { Nombre = "Mal", GastoMinimoRequerido = 0.001m })).Succeeded);
        var update = await service.UpdateAsync(f.Business.Id, saved.Id, f.EmployeeId, true, new UpdateTicketRequest { Nombre = "Sin condiciones", UsageWindows = [] });
        Assert.True(update.Succeeded);
        f.Db.ChangeTracker.Clear();
        saved = await f.Db.Tickets.SingleAsync(x => x.Id == saved.Id);
        Assert.Null(saved.GastoMinimoRequerido); Assert.Empty(saved.UsageWindows!);
    }

    private sealed class EventRecorder : IUserEventPublisher
    {
        public List<(Guid UserId, UserAppEvent Event)> Items { get; } = [];
        public Task PublishAsync(Guid userId, UserAppEvent appEvent, CancellationToken cancellationToken = default)
        { Items.Add((userId, appEvent)); return Task.CompletedTask; }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public DynamicFidelityDbContext Db { get; } = new(new DbContextOptionsBuilder<DynamicFidelityDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public DynamicNegociosDbContext BusinessDb { get; } = new(new DbContextOptionsBuilder<DynamicNegociosDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public Negocio Business { get; } = new() { Id = Guid.NewGuid(), NombreComercial = "Local", ZonaHoraria = "Europe/Madrid" };
        public Guid EmployeeId { get; } = Guid.NewGuid();
        public Ticket Ticket { get; } = new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Nombre = "Bono", CodigoVisible = "CODIGO", ExpiresAtUtc = DateTime.UtcNow.AddDays(7), Activo = true, EsDeUnSoloUso = true };
        public EventRecorder Events { get; } = new();
        public TicketQrService Service { get; private set; } = null!;
        public static async Task<Fixture> Create()
        {
            var f = new Fixture();
            f.Ticket.NegocioId = f.Business.Id;
            f.BusinessDb.Add(f.Business);
            f.BusinessDb.Add(new NegocioUsuarioVinculacion { Id = Guid.NewGuid(), NegocioId = f.Business.Id, UserId = f.EmployeeId,
                TipoVinculacion = TipoVinculacionNegocioUsuario.Trabajador, PuedeAccederBackoffice = true, PuedeValidarTickets = true });
            f.Db.Add(f.Ticket);
            await f.Db.SaveChangesAsync(); await f.BusinessDb.SaveChangesAsync();
            f.Service = new TicketQrService(f.Db, new TicketRepository(f.Db), null!, null!, new NegocioRepository(f.BusinessDb),
                new NegocioUsuarioVinculacionRepository(f.BusinessDb), new NegocioAudienciaService(f.BusinessDb, f.Db, null!),
                new TicketEventPublisher(f.Events), Options.Create(new FidelityQrOptions { TicketSigningSecret = "ticket-tests-secret-0123456789" }));
            return f;
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await BusinessDb.DisposeAsync(); }
    }
}
