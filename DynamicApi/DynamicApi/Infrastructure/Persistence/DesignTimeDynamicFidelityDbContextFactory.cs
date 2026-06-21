using Dynamic.Fidelity.Application.Options;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DynamicApi.Infrastructure.Persistence;

public class DesignTimeDynamicFidelityDbContextFactory : IDesignTimeDbContextFactory<DynamicFidelityDbContext>
{
    public DynamicFidelityDbContext CreateDbContext(string[] args)
    {
        string basePath = Directory.GetCurrentDirectory();

        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        DynamicFidelityDatabaseOptions databaseOptions = configuration
            .GetSection(DynamicFidelityDatabaseOptions.SectionName)
            .Get<DynamicFidelityDatabaseOptions>() ?? new();

        string connectionString = configuration.GetConnectionString(databaseOptions.ConnectionStringName)
            ?? throw new InvalidOperationException($"No existe la cadena de conexión '{databaseOptions.ConnectionStringName}'.");

        DbContextOptionsBuilder<DynamicFidelityDbContext> optionsBuilder = new();
        optionsBuilder.UseMySql(
            connectionString,
            new MariaDbServerVersion(Version.Parse(databaseOptions.MariaDbVersion)),
            mysql => mysql.MigrationsAssembly(typeof(Program).Assembly.GetName().Name));

        return new DynamicFidelityDbContext(optionsBuilder.Options);
    }
}
