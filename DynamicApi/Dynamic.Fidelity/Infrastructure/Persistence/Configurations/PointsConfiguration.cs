using Dynamic.Fidelity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dynamic.Fidelity.Infrastructure.Persistence.Configurations;

public class PointsConfiguration : IEntityTypeConfiguration<Points>
{
    public void Configure(EntityTypeBuilder<Points> builder)
    {
        builder.ToTable("fidelity_points");
        builder.HasKey(points => points.Id);

        builder.Property(points => points.LastReason).HasMaxLength(512);
        builder.Property(points => points.LastReference).HasMaxLength(256);

        builder.HasIndex(points => new { points.UserId, points.NegocioId }).IsUnique();
        builder.HasIndex(points => points.UserId);
        builder.HasIndex(points => points.NegocioId);
    }
}
