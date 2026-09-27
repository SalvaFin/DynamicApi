using Dynamic.Fidelity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dynamic.Fidelity.Infrastructure.Persistence.Configurations;

public sealed class RecurrenceRuleConfiguration : IEntityTypeConfiguration<RecurrenceRule>
{
    public void Configure(EntityTypeBuilder<RecurrenceRule> builder)
    {
        builder.ToTable("fidelity_recurrence_rules");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Family).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(x => x.BenefitMode).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(x => x.Compatibility).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.Multiplier).HasPrecision(8, 3);
        builder.HasIndex(x => new { x.NegocioId, x.Active, x.Priority });
    }
}
