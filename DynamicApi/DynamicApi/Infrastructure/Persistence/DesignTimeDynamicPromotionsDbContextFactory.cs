using Dynamic.Promotions.Application.Options;
using Dynamic.Promotions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DynamicApi.Infrastructure.Persistence;

public class DesignTimeDynamicPromotionsDbContextFactory : IDesignTimeDbContextFactory<DynamicPromotionsDbContext>
{
    public DynamicPromotionsDbContext CreateDbContext(string[] args)
    {
        string basePath = Directory.GetCurrentDirectory();
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        DynamicPromotionsDatabaseOptions databaseOptions = configuration
            .GetSection(DynamicPromotionsDatabaseOptions.SectionName)
            .Get<DynamicPromotionsDatabaseOptions>() ?? new();

        string connectionString = configuration.GetConnectionString(databaseOptions.ConnectionStringName)
            ?? throw new InvalidOperationException($"No existe la cadena de conexion '{databaseOptions.ConnectionStringName}'.");

        DbContextOptionsBuilder<DynamicPromotionsDbContext> optionsBuilder = new();
        optionsBuilder.UseMySql(
            connectionString,
            new MariaDbServerVersion(Version.Parse(databaseOptions.MariaDbVersion)),
            options => options.MigrationsAssembly(typeof(Program).Assembly.GetName().Name));

        return new DynamicPromotionsDbContext(optionsBuilder.Options);
    }
}
