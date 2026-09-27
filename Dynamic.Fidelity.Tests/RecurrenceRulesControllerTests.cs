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

public sealed class RecurrenceRulesControllerTests
{
    [Fact]
    public async Task OwnerCanCreateOnlyWithinOwnBusiness()
    {
        Guid owner = Guid.NewGuid();
        Guid ownBusiness = Guid.NewGuid();
        Guid otherBusiness = Guid.NewGuid();
        await using var fidelity = new DynamicFidelityDbContext(new DbContextOptionsBuilder<DynamicFidelityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await using var negocios = new DynamicNegociosDbContext(new DbContextOptionsBuilder<DynamicNegociosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        negocios.Negocios.AddRange(new Negocio { Id = ownBusiness, RatioConversionEurosAPuntos = 1 },
            new Negocio { Id = otherBusiness });
        negocios.NegociosUsuariosVinculaciones.Add(new NegocioUsuarioVinculacion
        {
            Id = Guid.NewGuid(), NegocioId = ownBusiness, UserId = owner,
            TipoVinculacion = TipoVinculacionNegocioUsuario.Propietario,
            Activa = true, PuedeGestionarNegocio = true
        });
        await negocios.SaveChangesAsync();
        var controller = new RecurrenceRulesController(fidelity, negocios,
            new RecurrenceEvaluationService(fidelity))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, owner.ToString())], "test"))
                }
            }
        };
        var input = new RecurrenceRule
        {
            NegocioId = otherBusiness, Name = "Quinta visita", Active = true,
            Family = RecurrenceRuleFamily.Milestone,
            BenefitMode = RecurrenceBenefitMode.CurrentVisit,
            Threshold = 5, Multiplier = 2, StartsAtUtc = DateTime.UtcNow
        };
        Assert.IsType<ForbidResult>(await controller.Create(otherBusiness, input, default));
        Assert.Empty(fidelity.RecurrenceRules);
        Assert.IsType<CreatedAtActionResult>(await controller.Create(ownBusiness, input, default));
        Assert.Equal(ownBusiness, Assert.Single(fidelity.RecurrenceRules).NegocioId);
        var draft = new RecurrenceRule
        {
            Id = Guid.NewGuid(), Name = "Primera visita", Family = RecurrenceRuleFamily.Milestone,
            BenefitMode = RecurrenceBenefitMode.CurrentVisit, Threshold = 1, Multiplier = 3,
            StartsAtUtc = DateTime.UtcNow.AddMinutes(-1)
        };
        Assert.IsType<OkObjectResult>(await controller.Preview(ownBusiness,
            new RecurrencePreviewRequest(owner, 10, draft), default));
        Assert.Single(fidelity.RecurrenceRules);
    }
}
