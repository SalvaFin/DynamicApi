using Dynamic.Fidelity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dynamic.Fidelity.Infrastructure.Persistence.Configurations;

public class PointsOperationAttemptConfiguration : IEntityTypeConfiguration<PointsOperationAttempt>
{
    public void Configure(EntityTypeBuilder<PointsOperationAttempt> builder)
    {
        builder.ToTable("fidelity_points_operation_attempts");
        builder.HasKey(attempt => attempt.Id);

        builder.Property(attempt => attempt.FailureReason).HasMaxLength(256);

        builder.HasIndex(attempt => new { attempt.OperationId, attempt.AttemptNumber }).IsUnique();
        builder.HasIndex(attempt => attempt.NegocioId);
        builder.HasIndex(attempt => attempt.CreatedAtUtc);
    }
}
