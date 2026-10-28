using Dynamic.Fidelity.Application.DTOs.Requests;
using Dynamic.Fidelity.Application.Services;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Dynamic.Fidelity.Infrastructure.Repositories;
using Dynamic.Negocios.Domain.Entities;
using Dynamic.Negocios.Infrastructure.Persistence;
using Dynamic.Negocios.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Dynamic.Fidelity.Tests;

public class TicketUsageLimitTests
{
    [Theory]
    [InlineData(0, true, null, false)]
    [InlineData(0, false, null, false)]
    [InlineData(1, false, 1, true)]
    [InlineData(3, true, 3, false)]
    [InlineData(null, true, 1, true)]
    [InlineData(null, false, null, false)]
    public async Task CreatePersistsUnlimitedOrPositiveLimitAndPreservesLegacyNull(
        int? requestedLimit, bool requestedSingleUse, int? expectedLimit, bool expectedSingleUse)
    {
        await using var fidelity = new DynamicFidelityDbContext(new DbContextOptionsBuilder<DynamicFidelityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await using var businesses = new DynamicNegociosDbContext(new DbContextOptionsBuilder<DynamicNegociosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var business = new Negocio { Id = Guid.NewGuid(), NombreComercial = "Local" };
        businesses.Negocios.Add(business);
        await businesses.SaveChangesAsync();
        var service = new TicketService(fidelity, businesses, new TicketRepository(fidelity), null!,
            new NegocioRepository(businesses), null!, null!, null!);
        var result = await service.CreateAsync(business.Id, Guid.NewGuid(), true, new CreateTicketRequest
        {
            Nombre = "Ticket", MaxUsosPorCliente = requestedLimit, EsDeUnSoloUso = requestedSingleUse
        });
        Assert.True(result.Succeeded, result.ErrorMessage);
        fidelity.ChangeTracker.Clear();
        var saved = await fidelity.Tickets.SingleAsync();
        // NULL disables the store acquisition cap and, together with false, the redemption cap.
        Assert.Equal(expectedLimit, saved.MaxUsosPorCliente);
        Assert.Equal(expectedSingleUse, saved.EsDeUnSoloUso);

        var updated = await service.UpdateAsync(business.Id, saved.Id, Guid.NewGuid(), true, new UpdateTicketRequest
        {
            Nombre = "Ticket editado", MaxUsosPorCliente = 0, EsDeUnSoloUso = true
        });
        Assert.True(updated.Succeeded, updated.ErrorMessage);
        fidelity.ChangeTracker.Clear();
        saved = await fidelity.Tickets.SingleAsync();
        Assert.Null(saved.MaxUsosPorCliente);
        Assert.False(saved.EsDeUnSoloUso);
    }
}
