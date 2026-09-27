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
    public DbSet<PointsTransaction> PointsTransactions => Set<PointsTransaction>();
    public DbSet<RecurrenceRule> RecurrenceRules => Set<RecurrenceRule>();
    public DbSet<PointsSplit> PointsSplits => Set<PointsSplit>();
    public DbSet<PointsSplitRecipient> PointsSplitRecipients => Set<PointsSplitRecipient>();
    public DbSet<PointsGroupAccrual> PointsGroupAccruals => Set<PointsGroupAccrual>();
    public DbSet<PointsGroupAccrualRecipient> PointsGroupAccrualRecipients => Set<PointsGroupAccrualRecipient>();
    public DbSet<PointsOperation> PointsOperations => Set<PointsOperation>();
    public DbSet<PointsOperationAttempt> PointsOperationAttempts => Set<PointsOperationAttempt>();
    public DbSet<UserCodeDirectoryEntry> UserCodeDirectoryEntries => Set<UserCodeDirectoryEntry>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketRedemption> TicketRedemptions => Set<TicketRedemption>();
    public DbSet<QrCampaign> QrCampaigns => Set<QrCampaign>();
    public DbSet<PendingTicketAssignment> PendingTicketAssignments => Set<PendingTicketAssignment>();
    public DbSet<WelcomeTicketClaim> WelcomeTicketClaims => Set<WelcomeTicketClaim>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DynamicFidelityDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
