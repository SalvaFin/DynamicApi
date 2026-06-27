using Dynamic.Users.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dynamic.Users.Infrastructure.Persistence.Configurations;

public class UserExternalLoginConfiguration : IEntityTypeConfiguration<UserExternalLogin>
{
    public void Configure(EntityTypeBuilder<UserExternalLogin> builder)
    {
        builder.ToTable("user_external_logins");
        builder.HasKey(login => login.Id);

        builder.Property(login => login.Provider).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(login => login.ProviderSubject).HasMaxLength(256).IsRequired();
        builder.Property(login => login.Email).HasMaxLength(256);
        builder.Property(login => login.DisplayName).HasMaxLength(128);

        builder.HasIndex(login => new { login.Provider, login.ProviderSubject }).IsUnique();
        builder.HasIndex(login => login.UserId);

        builder.HasOne(login => login.User)
            .WithMany(user => user.ExternalLogins)
            .HasForeignKey(login => login.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
