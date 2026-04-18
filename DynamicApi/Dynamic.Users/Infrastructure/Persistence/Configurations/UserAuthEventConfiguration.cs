using Dynamic.Users.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dynamic.Users.Infrastructure.Persistence.Configurations;

public class UserAuthEventConfiguration : IEntityTypeConfiguration<UserAuthEvent>
{
    public void Configure(EntityTypeBuilder<UserAuthEvent> builder)
    {
        builder.ToTable("user_auth_events");
        builder.HasKey(authEvent => authEvent.Id);

        builder.Property(authEvent => authEvent.EventType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(authEvent => authEvent.Identity).HasMaxLength(256);
        builder.Property(authEvent => authEvent.FailureReason).HasMaxLength(512);
        builder.Property(authEvent => authEvent.IpAddress).HasMaxLength(64);
        builder.Property(authEvent => authEvent.UserAgent).HasMaxLength(1024);
        builder.Property(authEvent => authEvent.ClientSummary).HasMaxLength(256);

        builder.HasIndex(authEvent => authEvent.UserId);
        builder.HasIndex(authEvent => authEvent.CreatedAtUtc);
    }
}
