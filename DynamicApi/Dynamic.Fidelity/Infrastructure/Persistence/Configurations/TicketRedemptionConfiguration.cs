using Dynamic.Fidelity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dynamic.Fidelity.Infrastructure.Persistence.Configurations;

public class TicketRedemptionConfiguration : IEntityTypeConfiguration<TicketRedemption>
{
    public void Configure(EntityTypeBuilder<TicketRedemption> builder)
    {
        builder.ToTable("fidelity_ticket_redemptions");
        builder.HasKey(redemption => redemption.Id);

        builder.Property(redemption => redemption.TicketNombreSnapshot).HasMaxLength(180).IsRequired();
        builder.Property(redemption => redemption.TicketTipoSnapshot).HasMaxLength(32).IsRequired();
        builder.Property(redemption => redemption.TicketCategoriaSnapshot).HasMaxLength(48).IsRequired();
        builder.Property(redemption => redemption.TicketCodeSnapshot).HasMaxLength(64);
        builder.Property(redemption => redemption.PurchaseAmount).HasPrecision(10, 2);
        builder.Property(redemption => redemption.DiscountAmount).HasPrecision(10, 2);
        builder.Property(redemption => redemption.FinalAmount).HasPrecision(10, 2);
        builder.Property(redemption => redemption.StoreReference).HasMaxLength(128);

        builder.HasIndex(redemption => redemption.TicketId);
        builder.HasIndex(redemption => redemption.NegocioId);
        builder.HasIndex(redemption => redemption.UserId);
        builder.HasIndex(redemption => redemption.ValidatedByUserId);
        builder.HasIndex(redemption => redemption.ParentTicketId);
        builder.HasIndex(redemption => redemption.SourceQrCampaignId);
        builder.HasIndex(redemption => redemption.SourcePromotionCampaignId);
        builder.HasIndex(redemption => redemption.SourcePromotionRecipientId);
        builder.HasIndex(redemption => redemption.CreatedAtUtc);
        builder.HasIndex(redemption => new { redemption.NegocioId, redemption.CreatedAtUtc });
        builder.HasIndex(redemption => new { redemption.TicketId, redemption.UsageNumber }).IsUnique();
    }
}
