using Dynamic.Users.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dynamic.Users.Infrastructure.Persistence.Configurations;

public class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("user_sessions");
        builder.HasKey(session => session.Id);

        builder.Property(session => session.JwtId).HasMaxLength(64).IsRequired();
        builder.Property(session => session.RefreshTokenHash).HasMaxLength(128).IsRequired();
        builder.Property(session => session.IpAddress).HasMaxLength(64);
        builder.Property(session => session.UserAgent).HasMaxLength(1024);
        builder.Property(session => session.AppName).HasMaxLength(128);
        builder.Property(session => session.AppVersion).HasMaxLength(64);
        builder.Property(session => session.RevocationReason).HasMaxLength(256);

        builder.HasIndex(session => session.UserId);
        builder.HasIndex(session => session.RefreshTokenHash).IsUnique();
        builder.HasIndex(session => session.JwtId).IsUnique();

        builder.HasOne(session => session.UserDevice)
            .WithMany(device => device.Sessions)
            .HasForeignKey(session => session.UserDeviceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
