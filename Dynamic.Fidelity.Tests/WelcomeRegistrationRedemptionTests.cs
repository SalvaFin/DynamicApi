using System.Security.Claims;
using Dynamic.Fidelity.Application.DTOs.Responses;
using Dynamic.Fidelity.Application.Options;
using Dynamic.Fidelity.Application.Services;
using Dynamic.Fidelity.Controllers;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Domain.Enums;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Dynamic.Fidelity.Infrastructure.Repositories;
using Dynamic.Negocios.Domain.Entities;
using Dynamic.Negocios.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Dynamic.Fidelity.Tests;

public sealed class WelcomeRegistrationRedemptionTests
{
    private static Ticket Welcome() => new()
    {
        Id = Guid.NewGuid(), NegocioId = Guid.NewGuid(), UserId = Guid.NewGuid(),
        ParentTicketId = Guid.NewGuid(), CategoriaEnvioEspecial = CategoriaEnvioTicket.PrimerRegistro,
        Nombre = "Cerveza de bienvenida", CodigoVisible = "WELCOME-TEST", Publicado = true,
        CreatedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow.AddDays(30)
    };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AlreadyEnjoyedWelcomeIsAuditedAndVisibleAsRedeemedAndCannotGenerateAnotherQr(bool singleUse)
    {
        await using var fidelity = new DynamicFidelityDbContext(new DbContextOptionsBuilder<DynamicFidelityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await using var businesses = new DynamicNegociosDbContext(new DbContextOptionsBuilder<DynamicNegociosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var ticket = Welcome();
        ticket.EsDeUnSoloUso = singleUse;
        ticket.MaxUsosPorCliente = singleUse ? 1 : 3;
        var employee = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var redemption = ticket.RedeemWelcomeAtRegistration(employee, now);
        fidelity.Tickets.Add(ticket);
        fidelity.TicketRedemptions.Add(redemption);
        fidelity.WelcomeTicketClaims.Add(new WelcomeTicketClaim
        {
            UserId = ticket.UserId!.Value, NegocioId = ticket.NegocioId, TicketId = ticket.Id, CreatedAtUtc = now
        });
        await fidelity.SaveChangesAsync();
        fidelity.ChangeTracker.Clear();
        var saved = Assert.Single(await fidelity.Tickets.ToListAsync());
        var audit = Assert.Single(await fidelity.TicketRedemptions.ToListAsync());
        Assert.True(saved.Usado);
        Assert.Equal(1, saved.UsosConsumidos);
        Assert.Equal(now, saved.UsedAtUtc);
        Assert.Equal(employee.ToString("D"), saved.UsedByEmployeeReference);
        Assert.Equal(saved.Id, audit.TicketId);
        Assert.Equal(saved.UserId, audit.UserId);
        Assert.Equal(saved.NegocioId, audit.NegocioId);
        Assert.Equal(employee, audit.ValidatedByUserId);
        Assert.Equal(saved.ParentTicketId, audit.ParentTicketId);
        Assert.Equal(saved.Nombre, audit.TicketNombreSnapshot);
        Assert.Equal("PrimerRegistro", audit.TicketCategoriaSnapshot);
        Assert.Equal(saved.CodigoVisible, audit.TicketCodeSnapshot);
        Assert.Equal(1, audit.UsageNumber);
        Assert.Equal(now, audit.CreatedAtUtc);
        Assert.Null(audit.PurchaseAmount);
        Assert.Null(audit.DiscountAmount);
        Assert.Null(audit.FinalAmount);
        Assert.Null(audit.MinimumSpendSatisfied);
        Assert.Single(await fidelity.WelcomeTicketClaims.ToListAsync());
        Assert.Throws<InvalidOperationException>(() => saved.RedeemWelcomeAtRegistration(employee, now));

        businesses.Negocios.Add(new Negocio { Id = saved.NegocioId, NombreComercial = "Local" });
        await businesses.SaveChangesAsync();
        var qrService = new TicketQrService(fidelity, new TicketRepository(fidelity), null!, null!, null!, null!,
            null!, null!, Options.Create(new FidelityQrOptions()));
        var qr = await qrService.GenerateAssignedTicketQrAsync(saved.Id, saved.UserId!.Value);
        Assert.False(qr.Succeeded);
        Assert.Equal("conflict", qr.ErrorCode);

        var portal = new UserPortalController(fidelity, businesses, qrService, null!, null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, saved.UserId.Value.ToString())], "test"))
                }
            }
        };
        var response = Assert.IsType<OkObjectResult>(await portal.GetMyTicket(saved.Id, default));
        var detail = Assert.IsType<UserPortalTicketResponse>(response.Value);
        Assert.Equal("redeemed", detail.Estado);
        Assert.True(detail.Usado);
        Assert.Equal(saved.CreatedAtUtc, detail.FechaAltaUtc);
        Assert.Equal(now, detail.FechaCanjeUtc);
        Assert.Equal(1, detail.ProgresoActual);
    }

    [Theory]
    [InlineData("template")]
    [InlineData("general")]
    [InlineData("unassigned")]
    [InlineData("used")]
    [InlineData("previous-use")]
    public void RegistrationRedemptionRejectsTicketsThatAreNotFreshAssignedWelcomes(string state)
    {
        var ticket = Welcome();
        switch (state)
        {
            case "template": ticket.EsPlantilla = true; break;
            case "general": ticket.CategoriaEnvioEspecial = CategoriaEnvioTicket.General; break;
            case "unassigned": ticket.UserId = null; break;
            case "used": ticket.Usado = true; break;
            case "previous-use": ticket.UsosConsumidos = 1; break;
        }
        Assert.Throws<InvalidOperationException>(() => ticket.RedeemWelcomeAtRegistration(Guid.NewGuid(), DateTime.UtcNow));
    }

    [Fact]
    public void EmployeeIdentityIsRequiredBeforeChangingTicket()
    {
        var ticket = Welcome();
        Assert.Throws<ArgumentException>(() => ticket.RedeemWelcomeAtRegistration(Guid.Empty, DateTime.UtcNow));
        Assert.False(ticket.Usado);
        Assert.Equal(0, ticket.UsosConsumidos);
    }
}
