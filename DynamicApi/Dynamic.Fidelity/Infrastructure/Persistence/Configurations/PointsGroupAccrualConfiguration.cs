using Dynamic.Fidelity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dynamic.Fidelity.Infrastructure.Persistence.Configurations;

public class PointsGroupAccrualConfiguration : IEntityTypeConfiguration<PointsGroupAccrual>
{
    public void Configure(EntityTypeBuilder<PointsGroupAccrual> builder)
    {
        builder.ToTable("fidelity_points_group_accruals");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.Property(x => x.AmountEuros).HasPrecision(10, 2);
        builder.HasMany(x => x.Recipients).WithOne(x => x.GroupAccrual)
            .HasForeignKey(x => x.GroupAccrualId).OnDelete(DeleteBehavior.Cascade);

    }
}

public class PointsGroupAccrualRecipientConfiguration : IEntityTypeConfiguration<PointsGroupAccrualRecipient>
{
    public void Configure(EntityTypeBuilder<PointsGroupAccrualRecipient> builder)
    {
        builder.ToTable("fidelity_points_group_accrual_recipients");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.GroupAccrualId, x.UserId }).IsUnique();
        builder.HasIndex(x => x.TransactionId).IsUnique();
    }
}
