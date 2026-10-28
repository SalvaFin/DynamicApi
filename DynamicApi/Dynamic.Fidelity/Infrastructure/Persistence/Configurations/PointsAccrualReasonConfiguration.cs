using Dynamic.Fidelity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dynamic.Fidelity.Infrastructure.Persistence.Configurations;

public class PointsAccrualReasonConfiguration : IEntityTypeConfiguration<PointsAccrualReason>
{
    public void Configure(EntityTypeBuilder<PointsAccrualReason> builder)
    {
        builder.ToTable("fidelity_points_accrual_reasons");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Ignore(x => x.IsDefault);
        builder.HasIndex(x => new { x.NegocioId, x.Name }).IsUnique();
    }
}
