using Dynamic.Negocios.Application.Options;
using Dynamic.Negocios.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DynamicApi.Infrastructure.Persistence;

public class DesignTimeDynamicNegociosDbContextFactory : IDesignTimeDbContextFactory<DynamicNegociosDbContext>
{
    public DynamicNegociosDbContext CreateDbContext(string[] args)
    {
        string basePath = Directory.GetCurrentDirectory();

        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        DynamicNegociosDatabaseOptions databaseOptions = configuration
            .GetSection(DynamicNegociosDatabaseOptions.SectionName)
            .Get<DynamicNegociosDatabaseOptions>() ?? new();

        string connectionString = configuration.GetConnectionString(databaseOptions.ConnectionStringName)
            ?? throw new InvalidOperationException($"No existe la cadena de conexión '{databaseOptions.ConnectionStringName}'.");

        DbContextOptionsBuilder<DynamicNegociosDbContext> optionsBuilder = new();
        optionsBuilder.UseMySql(
            connectionString,
            new MariaDbServerVersion(Version.Parse(databaseOptions.MariaDbVersion)),
            mysql => mysql.MigrationsAssembly(typeof(Program).Assembly.GetName().Name));

        return new DynamicNegociosDbContext(optionsBuilder.Options);
    }
}
