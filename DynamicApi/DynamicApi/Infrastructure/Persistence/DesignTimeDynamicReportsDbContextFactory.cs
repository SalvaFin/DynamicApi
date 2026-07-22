using Dynamic.Reports.Application.Options;
using Dynamic.Reports.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DynamicApi.Infrastructure.Persistence;

public sealed class DesignTimeDynamicReportsDbContextFactory : IDesignTimeDbContextFactory<DynamicReportsDbContext>
{
    public DynamicReportsDbContext CreateDbContext(string[] args)
    {
        string basePath = Directory.GetCurrentDirectory();
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        DynamicReportsDatabaseOptions databaseOptions = configuration
            .GetSection(DynamicReportsDatabaseOptions.SectionName)
            .Get<DynamicReportsDatabaseOptions>() ?? new();

        string connectionString = configuration.GetConnectionString(databaseOptions.ConnectionStringName)
            ?? throw new InvalidOperationException($"No existe la cadena de conexion '{databaseOptions.ConnectionStringName}'.");

        DbContextOptionsBuilder<DynamicReportsDbContext> optionsBuilder = new();
        optionsBuilder.UseMySql(
            connectionString,
            new MariaDbServerVersion(Version.Parse(databaseOptions.MariaDbVersion)),
            options => options.MigrationsAssembly(typeof(Program).Assembly.GetName().Name));

        return new DynamicReportsDbContext(optionsBuilder.Options);
    }
}
