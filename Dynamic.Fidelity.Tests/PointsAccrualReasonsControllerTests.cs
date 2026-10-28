using System.Security.Claims;
using Dynamic.Fidelity.Application.Services;
using Dynamic.Fidelity.Controllers;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Dynamic.Negocios.Domain.Entities;
using Dynamic.Negocios.Domain.Enums;
using Dynamic.Negocios.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Dynamic.Fidelity.Tests;

public sealed class PointsAccrualReasonsControllerTests
{
    [Fact]
    public async Task OwnerCanConfigureOnlyOwnBusinessAndDefaultCannotBeRemovedOrRenamed()
    {
        await using var db = Fidelity();
        await using var negocios = Businesses();
        Guid own = Guid.NewGuid(), other = Guid.NewGuid(), user = Guid.NewGuid();
        negocios.Negocios.AddRange(new Negocio { Id = own }, new Negocio { Id = other });
        negocios.NegociosUsuariosVinculaciones.Add(Link(own, user, true));
        await negocios.SaveChangesAsync();
        var controller = Controller(db, negocios, user);
        var request = new SavePointsAccrualReasonRequest { Name = "Reseña en Google", UniquePerCustomer = true };
        Assert.IsType<ForbidResult>(await controller.Create(other, request, default));
        var result = Assert.IsType<CreatedAtActionResult>(await controller.Create(own, request, default));
        var reason = Assert.IsType<PointsAccrualReason>(result.Value);
        Assert.Equal(own, reason.NegocioId);
        Assert.True(reason.UniquePerCustomer);
        Assert.IsType<BadRequestObjectResult>(await controller.Create(own, request, default));
        Assert.IsType<BadRequestObjectResult>(await controller.Update(own, own,
            new() { Name = "Paso por caja", Active = false }, default));
        Assert.IsType<BadRequestObjectResult>(await controller.Update(own, own,
            new() { Name = "Otro nombre" }, default));
        Assert.IsType<OkObjectResult>(await controller.Update(own, own,
            new() { Name = "Paso por caja", UniquePerCustomer = true }, default));
        Assert.True((await new PointsAccrualReasonService(db).ResolveAsync(own, null, default)).Data!.UniquePerCustomer);
    }

    [Fact]
    public async Task WorkerCanReadActiveReasonsButCannotConfigureOrReadAnotherBusiness()
    {
        await using var db = Fidelity();
        await using var negocios = Businesses();
        Guid own = Guid.NewGuid(), other = Guid.NewGuid(), user = Guid.NewGuid();
        negocios.Negocios.AddRange(new Negocio { Id = own }, new Negocio { Id = other });
        var link = Link(own, user, false);
        negocios.NegociosUsuariosVinculaciones.Add(link);
        await negocios.SaveChangesAsync();
        var controller = Controller(db, negocios, user);
        Assert.IsType<OkObjectResult>(await controller.List(own, false, default));
        Assert.IsType<ForbidResult>(await controller.List(own, true, default));
        Assert.IsType<ForbidResult>(await controller.List(other, false, default));
        Assert.IsType<ForbidResult>(await controller.Create(own, new() { Name = "Reseña" }, default));
        Assert.IsType<ForbidResult>(await controller.Update(own, own, new() { Name = "Paso por caja" }, default));
        link.RevokedAtUtc = DateTime.UtcNow;
        await negocios.SaveChangesAsync();
        Assert.IsType<ForbidResult>(await controller.List(own, false, default));
    }

    [Fact]
    public async Task EditingPreservesIdentityAndBusinessBoundaryAndRejectsBlankOrReservedNames()
    {
        await using var db = Fidelity();
        await using var negocios = Businesses();
        Guid own = Guid.NewGuid(), other = Guid.NewGuid(), user = Guid.NewGuid();
        negocios.Negocios.AddRange(new Negocio { Id = own }, new Negocio { Id = other });
        negocios.NegociosUsuariosVinculaciones.AddRange(Link(own, user, true), Link(other, user, true));
        await negocios.SaveChangesAsync();
        var reason = new PointsAccrualReason { Id = Guid.NewGuid(), NegocioId = own, Name = "Reseña", UniquePerCustomer = true };
        db.PointsAccrualReasons.Add(reason);
        await db.SaveChangesAsync();
        var controller = Controller(db, negocios, user);
        Assert.IsType<NotFoundResult>(await controller.Update(other, reason.Id, new() { Name = "Ajeno" }, default));
        Assert.IsType<BadRequestObjectResult>(await controller.Create(own, new() { Name = " paso por caja " }, default));
        Assert.IsType<BadRequestObjectResult>(await controller.Create(own, new() { Name = " " }, default));
        var saved = Assert.IsType<OkObjectResult>(await controller.Update(own, reason.Id,
            new() { Name = "Reseña verificada", Active = false, UniquePerCustomer = true }, default));
        var updated = Assert.IsType<PointsAccrualReason>(saved.Value);
        Assert.Equal(reason.Id, updated.Id);
        Assert.Equal(own, updated.NegocioId);
        Assert.False(updated.Active);
        Assert.True(updated.UniquePerCustomer);
    }

    private static DynamicFidelityDbContext Fidelity() => new(new DbContextOptionsBuilder<DynamicFidelityDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static DynamicNegociosDbContext Businesses() => new(new DbContextOptionsBuilder<DynamicNegociosDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static NegocioUsuarioVinculacion Link(Guid business, Guid user, bool owner) => new()
    {
        Id = Guid.NewGuid(), NegocioId = business, UserId = user, Activa = true,
        TipoVinculacion = owner ? TipoVinculacionNegocioUsuario.Propietario : TipoVinculacionNegocioUsuario.Trabajador,
        PuedeGestionarNegocio = owner, PuedeGestionarPuntos = true
    };
    private static PointsAccrualReasonsController Controller(DynamicFidelityDbContext db,
        DynamicNegociosDbContext negocios, Guid user) => new(db, negocios, new PointsAccrualReasonService(db))
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.ToString())], "test"))
        } }
    };
}
