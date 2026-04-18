using Dynamic.Users.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dynamic.Users.Infrastructure.Persistence.Configurations;

public class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("users");
        builder.HasKey(user => user.Id);

        builder.Property(user => user.Email).HasMaxLength(256);
        builder.Property(user => user.NormalizedEmail).HasMaxLength(256);
        builder.Property(user => user.UserName).HasMaxLength(64).IsRequired();
        builder.Property(user => user.NormalizedUserName).HasMaxLength(64).IsRequired();
        builder.Property(user => user.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(user => user.FirstName).HasMaxLength(128);
        builder.Property(user => user.LastName).HasMaxLength(128);
        builder.Property(user => user.DisplayName).HasMaxLength(128);
        builder.Property(user => user.PhoneNumber).HasMaxLength(32);
        builder.Property(user => user.NormalizedPhoneNumber).HasMaxLength(32);
        builder.Property(user => user.RegistrationValidationToken).HasMaxLength(128);
        builder.Property(user => user.Language).HasMaxLength(16);
        builder.Property(user => user.TimeZone).HasMaxLength(64);
        builder.Property(user => user.CountryCode).HasMaxLength(8);
        builder.Property(user => user.Region).HasMaxLength(128);
        builder.Property(user => user.City).HasMaxLength(128);
        builder.Property(user => user.AvatarUrl).HasMaxLength(512);
        builder.Property(user => user.LastLoginIp).HasMaxLength(64);
        builder.Property(user => user.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(user => user.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.HasIndex(user => user.NormalizedEmail).IsUnique();
        builder.HasIndex(user => user.NormalizedUserName).IsUnique();
        builder.HasIndex(user => user.NormalizedPhoneNumber).IsUnique();
        builder.HasIndex(user => user.RegistrationValidationToken);

        builder.HasMany(user => user.Devices)
            .WithOne(device => device.User)
            .HasForeignKey(device => device.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(user => user.Sessions)
            .WithOne(session => session.User)
            .HasForeignKey(session => session.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(user => user.AuthEvents)
            .WithOne(authEvent => authEvent.User)
            .HasForeignKey(authEvent => authEvent.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
