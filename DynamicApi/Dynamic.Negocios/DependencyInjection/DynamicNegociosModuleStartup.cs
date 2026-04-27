using System.Reflection;
using Dynamic.Negocios.Application.Contracts.Repositories;
using Dynamic.Negocios.Application.Contracts.Services;
using Dynamic.Negocios.Application.Options;
using Dynamic.Negocios.Application.Services;
using Dynamic.Negocios.Controllers;
using Dynamic.Negocios.Infrastructure.Persistence;
using Dynamic.Negocios.Infrastructure.Repositories;
using Dynamic.Negocios.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dynamic.Negocios.DependencyInjection;

public class DynamicNegociosModuleStartup
{
    public void RegisterModule(IServiceCollection services, IConfiguration configuration, IMvcBuilder mvcBuilder)
    {
        services.AddOptions<DynamicNegociosDatabaseOptions>()
            .Bind(configuration.GetSection(DynamicNegociosDatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<NegocioMediaOptions>()
            .Bind(configuration.GetSection(NegocioMediaOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        DynamicNegociosDatabaseOptions databaseOptions = configuration
            .GetSection(DynamicNegociosDatabaseOptions.SectionName)
            .Get<DynamicNegociosDatabaseOptions>() ?? new();

        string connectionString = configuration.GetConnectionString(databaseOptions.ConnectionStringName)
            ?? throw new InvalidOperationException($"No existe la cadena de conexión '{databaseOptions.ConnectionStringName}'.");

        services.AddDbContext<DynamicNegociosDbContext>(options =>
        {
            string migrationsAssembly = Assembly.GetEntryAssembly()?.GetName().Name
                ?? typeof(DynamicNegociosModuleStartup).Assembly.GetName().Name!;

            options.UseMySql(
                connectionString,
                new MariaDbServerVersion(Version.Parse(databaseOptions.MariaDbVersion)),
                mySqlOptions => mySqlOptions.MigrationsAssembly(migrationsAssembly));
        });

        services.AddScoped<INegocioRepository, NegocioRepository>();
        services.AddScoped<INegocioUsuarioVinculacionRepository, NegocioUsuarioVinculacionRepository>();
        services.AddScoped<INegocioMediaStorageService, NegocioMediaStorageService>();
        services.AddScoped<INegocioService, NegocioService>();
        services.AddScoped<INegocioUsuarioVinculacionService, NegocioUsuarioVinculacionService>();

        mvcBuilder.AddApplicationPart(typeof(NegociosController).Assembly);
    }
}
