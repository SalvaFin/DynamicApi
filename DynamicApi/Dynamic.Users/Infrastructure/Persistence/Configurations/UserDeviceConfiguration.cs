using Dynamic.Users.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dynamic.Users.Infrastructure.Persistence.Configurations;

public class UserDeviceConfiguration : IEntityTypeConfiguration<UserDevice>
{
    public void Configure(EntityTypeBuilder<UserDevice> builder)
    {
        builder.ToTable("user_devices");
        builder.HasKey(device => device.Id);

        builder.Property(device => device.ExternalDeviceId).HasMaxLength(128);
        builder.Property(device => device.InstallationId).HasMaxLength(128);
        builder.Property(device => device.DeviceFingerprint).HasMaxLength(256);
        builder.Property(device => device.DeviceName).HasMaxLength(128);
        builder.Property(device => device.Manufacturer).HasMaxLength(128);
        builder.Property(device => device.Model).HasMaxLength(128);
        builder.Property(device => device.OperatingSystem).HasMaxLength(64);
        builder.Property(device => device.OperatingSystemVersion).HasMaxLength(64);
        builder.Property(device => device.BrowserName).HasMaxLength(64);
        builder.Property(device => device.BrowserVersion).HasMaxLength(64);
        builder.Property(device => device.AppName).HasMaxLength(128);
        builder.Property(device => device.AppVersion).HasMaxLength(64);
        builder.Property(device => device.AppBuild).HasMaxLength(64);
        builder.Property(device => device.Locale).HasMaxLength(16);
        builder.Property(device => device.TimeZone).HasMaxLength(64);
        builder.Property(device => device.PushToken).HasMaxLength(1024);
        builder.Property(device => device.DeviceType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(device => device.Platform).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(device => device.PushProvider).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.HasIndex(device => new { device.UserId, device.DeviceFingerprint });
        builder.HasIndex(device => new { device.UserId, device.ExternalDeviceId });
        builder.HasIndex(device => new { device.UserId, device.InstallationId });
    }
}
