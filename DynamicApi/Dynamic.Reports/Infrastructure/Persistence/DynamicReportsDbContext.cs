using Dynamic.Reports.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dynamic.Reports.Infrastructure.Persistence;

public sealed class DynamicReportsDbContext : DbContext
{
    public DynamicReportsDbContext(DbContextOptions<DynamicReportsDbContext> options)
        : base(options)
    {
    }

    public DbSet<SupportReport> Reports => Set<SupportReport>();
    public DbSet<ReportEvent> ReportEvents => Set<ReportEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DynamicReportsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
