using Dynamic.Promotions.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dynamic.Promotions.Infrastructure.Persistence.Configurations;

public class PromotionRecipientConfiguration : IEntityTypeConfiguration<PromotionRecipient>
{
    public void Configure(EntityTypeBuilder<PromotionRecipient> builder)
    {
        builder.ToTable("promotion_recipients");
        builder.HasKey(recipient => recipient.Id);
        builder.Property(recipient => recipient.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.HasIndex(recipient => new { recipient.CampaignId, recipient.UserId }).IsUnique();
        builder.HasIndex(recipient => new { recipient.UserId, recipient.ReceivedAtUtc });
        builder.HasIndex(recipient => new { recipient.UserId, recipient.Status, recipient.ExpiresAtUtc });
        builder.HasOne(recipient => recipient.Campaign)
            .WithMany(campaign => campaign.Recipients)
            .HasForeignKey(recipient => recipient.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
