using Dynamic.Promotions.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dynamic.Promotions.Infrastructure.Persistence.Configurations;

public class PromotionCampaignConfiguration : IEntityTypeConfiguration<PromotionCampaign>
{
    public void Configure(EntityTypeBuilder<PromotionCampaign> builder)
    {
        builder.ToTable("promotion_campaigns");
        builder.HasKey(campaign => campaign.Id);
        builder.Property(campaign => campaign.TicketTemplateId).IsRequired();
        builder.Property(campaign => campaign.NegocioNombreSnapshot).HasMaxLength(180).IsRequired();
        builder.Property(campaign => campaign.NegocioSlugSnapshot).HasMaxLength(180).IsRequired();
        builder.Property(campaign => campaign.NegocioLogoUrlSnapshot).HasMaxLength(1024);
        builder.Property(campaign => campaign.TicketNombreSnapshot).HasMaxLength(180).IsRequired();
        builder.Property(campaign => campaign.TicketDescripcionSnapshot).HasMaxLength(2000);
        builder.Property(campaign => campaign.TicketSnapshotJson).HasColumnType("longtext").IsRequired();
        builder.Property(campaign => campaign.FiltersJson).HasColumnType("longtext").IsRequired();
        builder.Property(campaign => campaign.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(campaign => campaign.IdempotencyKey).HasMaxLength(128);
        builder.Property(campaign => campaign.LastError).HasMaxLength(2000);
        builder.HasIndex(campaign => new { campaign.NegocioId, campaign.CreatedAtUtc });
        builder.HasIndex(campaign => new { campaign.NegocioId, campaign.TicketTemplateId });
        builder.HasIndex(campaign => new { campaign.Status, campaign.ScheduledAtUtc });
        builder.HasIndex(campaign => new { campaign.NegocioId, campaign.IdempotencyKey }).IsUnique();
    }
}
