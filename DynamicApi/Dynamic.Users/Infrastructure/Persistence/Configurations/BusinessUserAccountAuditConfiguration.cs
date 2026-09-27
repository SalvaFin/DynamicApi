using Dynamic.Users.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dynamic.Users.Infrastructure.Persistence.Configurations;

public sealed class BusinessUserAccountAuditConfiguration : IEntityTypeConfiguration<BusinessUserAccountAudit>
{
    public void Configure(EntityTypeBuilder<BusinessUserAccountAudit> builder)
    {
        builder.ToTable("business_user_account_audits");
        builder.HasKey(audit => audit.Id);
        builder.Property(audit => audit.ChangesJson).HasColumnType("json").IsRequired();
        builder.Property(audit => audit.IpAddress).HasMaxLength(64);
        builder.Property(audit => audit.UserAgent).HasMaxLength(1024);
        builder.HasIndex(audit => new { audit.NegocioId, audit.UserId, audit.CreatedAtUtc });
        builder.HasIndex(audit => audit.ModifiedByUserId);
    }
}
