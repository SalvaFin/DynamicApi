using Dynamic.Reports.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dynamic.Reports.Infrastructure.Persistence.Configurations;

public sealed class ReportEventConfiguration : IEntityTypeConfiguration<ReportEvent>
{
    public void Configure(EntityTypeBuilder<ReportEvent> builder)
    {
        builder.ToTable("support_report_events");
        builder.HasKey(reportEvent => reportEvent.Id);
        builder.Property(reportEvent => reportEvent.Kind).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(reportEvent => reportEvent.Message).HasMaxLength(5000);
        builder.Property(reportEvent => reportEvent.PreviousStatus).HasConversion<string>().HasMaxLength(24);
        builder.Property(reportEvent => reportEvent.NewStatus).HasConversion<string>().HasMaxLength(24);
        builder.Property(reportEvent => reportEvent.PreviousPriority).HasConversion<string>().HasMaxLength(16);
        builder.Property(reportEvent => reportEvent.NewPriority).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(reportEvent => new { reportEvent.ReportId, reportEvent.CreatedAtUtc });
    }
}
