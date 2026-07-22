using System.Reflection;
using Dynamic.Reports.Application.Contracts;
using Dynamic.Reports.Application.Options;
using Dynamic.Reports.Application.Services;
using Dynamic.Reports.Controllers;
using Dynamic.Reports.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dynamic.Reports.DependencyInjection;

public sealed class DynamicReportsModuleStartup
{
    public void RegisterModule(IServiceCollection services, IConfiguration configuration, IMvcBuilder mvcBuilder)
    {
        services.AddOptions<DynamicReportsDatabaseOptions>()
            .Bind(configuration.GetSection(DynamicReportsDatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        DynamicReportsDatabaseOptions databaseOptions = configuration
            .GetSection(DynamicReportsDatabaseOptions.SectionName)
            .Get<DynamicReportsDatabaseOptions>() ?? new();

        string connectionString = configuration.GetConnectionString(databaseOptions.ConnectionStringName)
            ?? throw new InvalidOperationException($"No existe la cadena de conexión '{databaseOptions.ConnectionStringName}'.");

        services.AddDbContext<DynamicReportsDbContext>(options =>
        {
            string migrationsAssembly = Assembly.GetEntryAssembly()?.GetName().Name
                ?? typeof(DynamicReportsModuleStartup).Assembly.GetName().Name!;

            options.UseMySql(
                connectionString,
                new MariaDbServerVersion(Version.Parse(databaseOptions.MariaDbVersion)),
                mySqlOptions => mySqlOptions.MigrationsAssembly(migrationsAssembly));
        });

        services.AddScoped<IReportService, ReportService>();
        mvcBuilder.AddApplicationPart(typeof(UserReportsController).Assembly);
    }
}
