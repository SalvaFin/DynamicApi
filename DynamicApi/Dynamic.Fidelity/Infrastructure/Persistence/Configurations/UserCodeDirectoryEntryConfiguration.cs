using Dynamic.Fidelity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dynamic.Fidelity.Infrastructure.Persistence.Configurations;

public class UserCodeDirectoryEntryConfiguration : IEntityTypeConfiguration<UserCodeDirectoryEntry>
{
    public void Configure(EntityTypeBuilder<UserCodeDirectoryEntry> builder)
    {
        builder.ToTable("fidelity_user_codes");
        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.UserCode).HasMaxLength(32).IsRequired();

        builder.HasIndex(entry => entry.UserId).IsUnique();
        builder.HasIndex(entry => entry.UserCode).IsUnique();
    }
}
