using Dynamic.Promotions.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dynamic.Promotions.Infrastructure.Persistence.Configurations;

public class PromotionDeliveryConfiguration : IEntityTypeConfiguration<PromotionDelivery>
{
    public void Configure(EntityTypeBuilder<PromotionDelivery> builder)
    {
        builder.ToTable("promotion_deliveries");
        builder.HasKey(delivery => delivery.Id);
        builder.Property(delivery => delivery.Provider).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(delivery => delivery.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(delivery => delivery.ProviderMessageId).HasMaxLength(512);
        builder.Property(delivery => delivery.LastError).HasMaxLength(2000);
        builder.HasIndex(delivery => new { delivery.RecipientId, delivery.UserDeviceId }).IsUnique();
        builder.HasIndex(delivery => new { delivery.Status, delivery.NextAttemptAtUtc });
        builder.HasIndex(delivery => new { delivery.CampaignId, delivery.Status });
        builder.HasOne(delivery => delivery.Campaign)
            .WithMany(campaign => campaign.Deliveries)
            .HasForeignKey(delivery => delivery.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(delivery => delivery.Recipient)
            .WithMany(recipient => recipient.Deliveries)
            .HasForeignKey(delivery => delivery.RecipientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
