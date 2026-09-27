using Dynamic.Fidelity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dynamic.Fidelity.Infrastructure.Persistence.Configurations;

public class PointsSplitConfiguration : IEntityTypeConfiguration<PointsSplit>
{
    public void Configure(EntityTypeBuilder<PointsSplit> builder)
    {
        builder.ToTable("fidelity_points_splits");
        builder.HasKey(split => split.Id);
        builder.HasIndex(split => split.SourceTransactionId).IsUnique();
        builder.HasIndex(split => new { split.OwnerUserId, split.CreatedAtUtc });
        builder.HasMany(split => split.Recipients)
            .WithOne(recipient => recipient.Split)
            .HasForeignKey(recipient => recipient.SplitId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PointsSplitRecipientConfiguration : IEntityTypeConfiguration<PointsSplitRecipient>
{
    public void Configure(EntityTypeBuilder<PointsSplitRecipient> builder)
    {
        builder.ToTable("fidelity_points_split_recipients");
        builder.HasKey(recipient => recipient.Id);
        builder.HasIndex(recipient => new { recipient.SplitId, recipient.UserId }).IsUnique();
        builder.HasIndex(recipient => recipient.IncomingTransactionId).IsUnique();
    }
}
