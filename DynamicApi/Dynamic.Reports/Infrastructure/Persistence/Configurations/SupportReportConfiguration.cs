using Dynamic.Reports.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dynamic.Reports.Infrastructure.Persistence.Configurations;

public sealed class SupportReportConfiguration : IEntityTypeConfiguration<SupportReport>
{
    public void Configure(EntityTypeBuilder<SupportReport> builder)
    {
        builder.ToTable("support_reports");
        builder.HasKey(report => report.Id);
        builder.Property(report => report.Category).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(report => report.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(report => report.Priority).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(report => report.Subject).HasMaxLength(160).IsRequired();
        builder.Property(report => report.Description).HasMaxLength(5000).IsRequired();
        builder.Property(report => report.PageUrl).HasMaxLength(1000);
        builder.Property(report => report.AppVersion).HasMaxLength(64);

        builder.HasIndex(report => new { report.ReporterUserId, report.CreatedAtUtc });
        builder.HasIndex(report => new { report.Status, report.Priority, report.CreatedAtUtc });
        builder.HasIndex(report => new { report.AssignedAdminUserId, report.Status });
        builder.HasIndex(report => report.TicketId);
        builder.HasIndex(report => report.BusinessId);
        builder.HasIndex(report => report.PromotionCampaignId);

        builder.HasMany(report => report.Events)
            .WithOne(reportEvent => reportEvent.Report)
            .HasForeignKey(reportEvent => reportEvent.ReportId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
