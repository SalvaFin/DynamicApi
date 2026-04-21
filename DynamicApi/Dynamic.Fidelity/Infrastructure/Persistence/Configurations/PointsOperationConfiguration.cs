using Dynamic.Fidelity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dynamic.Fidelity.Infrastructure.Persistence.Configurations;

public class PointsOperationConfiguration : IEntityTypeConfiguration<PointsOperation>
{
    public void Configure(EntityTypeBuilder<PointsOperation> builder)
    {
        builder.ToTable("fidelity_points_operations");
        builder.HasKey(operation => operation.Id);

        builder.Property(operation => operation.AmountEuros).HasPrecision(10, 2);
        builder.Property(operation => operation.RatioSnapshot).HasPrecision(10, 4);
        builder.Property(operation => operation.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(operation => operation.CancelReason).HasMaxLength(256);

        builder.HasIndex(operation => new { operation.UserId, operation.NegocioId, operation.Status });
        builder.HasIndex(operation => operation.NegocioId);
    }
}
