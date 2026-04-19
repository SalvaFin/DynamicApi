using Dynamic.Negocios.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dynamic.Negocios.Infrastructure.Persistence;

public class DynamicNegociosDbContext : DbContext
{
    public DynamicNegociosDbContext(DbContextOptions<DynamicNegociosDbContext> options)
        : base(options)
    {
    }

    public DbSet<Negocio> Negocios => Set<Negocio>();
    public DbSet<NegocioUsuarioVinculacion> NegociosUsuariosVinculaciones => Set<NegocioUsuarioVinculacion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DynamicNegociosDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
