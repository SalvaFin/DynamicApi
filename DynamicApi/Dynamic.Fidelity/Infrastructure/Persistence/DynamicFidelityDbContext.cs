using Dynamic.Fidelity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dynamic.Fidelity.Infrastructure.Persistence;

public class DynamicFidelityDbContext : DbContext
{
    public DynamicFidelityDbContext(DbContextOptions<DynamicFidelityDbContext> options)
        : base(options)
    {
    }

    public DbSet<Points> Points => Set<Points>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<QrCampaign> QrCampaigns => Set<QrCampaign>();
    public DbSet<PendingTicketAssignment> PendingTicketAssignments => Set<PendingTicketAssignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DynamicFidelityDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
