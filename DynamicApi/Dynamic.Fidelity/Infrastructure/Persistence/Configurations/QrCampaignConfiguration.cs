using Dynamic.Fidelity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dynamic.Fidelity.Infrastructure.Persistence.Configurations;

public class QrCampaignConfiguration : IEntityTypeConfiguration<QrCampaign>
{
    public void Configure(EntityTypeBuilder<QrCampaign> builder)
    {
        builder.ToTable("fidelity_qr_campaigns");
        builder.HasKey(qrCampaign => qrCampaign.Id);

        builder.Property(qrCampaign => qrCampaign.Nombre).HasMaxLength(180).IsRequired();
        builder.Property(qrCampaign => qrCampaign.Token).HasMaxLength(128).IsRequired();
        builder.Property(qrCampaign => qrCampaign.Descripcion).HasMaxLength(2000);
        builder.Property(qrCampaign => qrCampaign.LandingPath).HasMaxLength(512);

        builder.HasIndex(qrCampaign => qrCampaign.Token).IsUnique();
        builder.HasIndex(qrCampaign => qrCampaign.NegocioId);
        builder.HasIndex(qrCampaign => qrCampaign.Activa);
    }
}
