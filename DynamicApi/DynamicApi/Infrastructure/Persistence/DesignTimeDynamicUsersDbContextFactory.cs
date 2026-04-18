using Dynamic.Users.Application.Options;
using Dynamic.Users.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DynamicApi.Infrastructure.Persistence;

public class DesignTimeDynamicUsersDbContextFactory : IDesignTimeDbContextFactory<DynamicUsersDbContext>
{
    public DynamicUsersDbContext CreateDbContext(string[] args)
    {
        string basePath = Directory.GetCurrentDirectory();

        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        DynamicUsersDatabaseOptions databaseOptions = configuration
            .GetSection(DynamicUsersDatabaseOptions.SectionName)
            .Get<DynamicUsersDatabaseOptions>() ?? new();

        string connectionString = configuration.GetConnectionString(databaseOptions.ConnectionStringName)
            ?? throw new InvalidOperationException($"No existe la cadena de conexión '{databaseOptions.ConnectionStringName}'.");

        DbContextOptionsBuilder<DynamicUsersDbContext> optionsBuilder = new();
        optionsBuilder.UseMySql(
            connectionString,
            new MariaDbServerVersion(Version.Parse(databaseOptions.MariaDbVersion)),
            mysql => mysql.MigrationsAssembly(typeof(Program).Assembly.GetName().Name));

        return new DynamicUsersDbContext(optionsBuilder.Options);
    }
}
