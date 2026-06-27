using Dynamic.Users.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dynamic.Users.Infrastructure.Persistence;

public class DynamicUsersDbContext : DbContext
{
    public DynamicUsersDbContext(DbContextOptions<DynamicUsersDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<UserDevice> UserDevices => Set<UserDevice>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<UserAuthEvent> UserAuthEvents => Set<UserAuthEvent>();
    public DbSet<UserExternalLogin> UserExternalLogins => Set<UserExternalLogin>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DynamicUsersDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
