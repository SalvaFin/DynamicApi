using Dynamic.Promotions.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dynamic.Promotions.Infrastructure.Persistence;

public class DynamicPromotionsDbContext : DbContext
{
    public DynamicPromotionsDbContext(DbContextOptions<DynamicPromotionsDbContext> options)
        : base(options)
    {
    }

    public DbSet<PromotionCampaign> Campaigns => Set<PromotionCampaign>();
    public DbSet<PromotionRecipient> Recipients => Set<PromotionRecipient>();
    public DbSet<PromotionDelivery> Deliveries => Set<PromotionDelivery>();
    public DbSet<PromotionEmailDelivery> EmailDeliveries => Set<PromotionEmailDelivery>();
    public DbSet<PromotionOutboxMessage> OutboxMessages => Set<PromotionOutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DynamicPromotionsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
