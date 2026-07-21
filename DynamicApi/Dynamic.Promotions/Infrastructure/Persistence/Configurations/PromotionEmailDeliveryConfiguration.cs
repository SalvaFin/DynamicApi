using Dynamic.Promotions.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dynamic.Promotions.Infrastructure.Persistence.Configurations;

public class PromotionEmailDeliveryConfiguration : IEntityTypeConfiguration<PromotionEmailDelivery>
{
    public void Configure(EntityTypeBuilder<PromotionEmailDelivery> builder)
    {
        builder.ToTable("promotion_email_deliveries");
        builder.HasKey(delivery => delivery.Id);
        builder.Property(delivery => delivery.Email).HasMaxLength(256).IsRequired();
        builder.Property(delivery => delivery.RecipientName).HasMaxLength(256);
        builder.Property(delivery => delivery.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(delivery => delivery.ProviderMessageId).HasMaxLength(512);
        builder.Property(delivery => delivery.LastError).HasMaxLength(2000);
        builder.HasIndex(delivery => delivery.RecipientId).IsUnique();
        builder.HasIndex(delivery => delivery.UnsubscribeToken).IsUnique();
        builder.HasIndex(delivery => new { delivery.Status, delivery.NextAttemptAtUtc });
        builder.HasIndex(delivery => new { delivery.CampaignId, delivery.Status });
        builder.HasOne(delivery => delivery.Campaign).WithMany(campaign => campaign.EmailDeliveries)
            .HasForeignKey(delivery => delivery.CampaignId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(delivery => delivery.Recipient).WithMany(recipient => recipient.EmailDeliveries)
            .HasForeignKey(delivery => delivery.RecipientId).OnDelete(DeleteBehavior.Cascade);
    }
}
