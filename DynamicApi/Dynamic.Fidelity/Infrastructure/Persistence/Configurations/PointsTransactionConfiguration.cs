using Dynamic.Fidelity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dynamic.Fidelity.Infrastructure.Persistence.Configurations;

public class PointsTransactionConfiguration : IEntityTypeConfiguration<PointsTransaction>
{
    public void Configure(EntityTypeBuilder<PointsTransaction> builder)
    {
        builder.ToTable("fidelity_points_transactions");
        builder.HasKey(transaction => transaction.Id);

        builder.Property(transaction => transaction.TransactionType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(transaction => transaction.AmountEuros).HasPrecision(10, 2);
        builder.Property(transaction => transaction.UserCodeSnapshot).HasMaxLength(32);
        builder.Property(transaction => transaction.Reason).HasMaxLength(512);
        builder.Property(transaction => transaction.Reference).HasMaxLength(256);

        builder.HasIndex(transaction => new { transaction.UserId, transaction.NegocioId, transaction.CreatedAtUtc });
        builder.HasIndex(transaction => transaction.NegocioId);
        builder.HasIndex(transaction => transaction.OperationId);
    }
}
