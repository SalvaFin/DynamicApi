using System.Reflection;
using Dynamic.Promotions.Application.Contracts;
using Dynamic.Promotions.Application.Options;
using Dynamic.Promotions.Application.Services;
using Dynamic.Promotions.Controllers;
using Dynamic.Promotions.Infrastructure.Persistence;
using Dynamic.Promotions.Infrastructure.Push;
using Dynamic.Promotions.Infrastructure.Workers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dynamic.Promotions.DependencyInjection;

public class DynamicPromotionsModuleStartup
{
    public void RegisterModule(IServiceCollection services, IConfiguration configuration, IMvcBuilder mvcBuilder)
    {
        services.AddOptions<DynamicPromotionsDatabaseOptions>()
            .Bind(configuration.GetSection(DynamicPromotionsDatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<PromotionDispatchOptions>()
            .Bind(configuration.GetSection(PromotionDispatchOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<FirebasePushOptions>()
            .Bind(configuration.GetSection(FirebasePushOptions.SectionName))
            .Validate(options =>
                !options.Enabled ||
                !string.IsNullOrWhiteSpace(options.ProjectId) && !string.IsNullOrWhiteSpace(options.ServiceAccountJson),
                "Promotions:Firebase requiere ProjectId y ServiceAccountJson cuando esta habilitado.")
            .ValidateOnStart();

        DynamicPromotionsDatabaseOptions databaseOptions = configuration
            .GetSection(DynamicPromotionsDatabaseOptions.SectionName)
            .Get<DynamicPromotionsDatabaseOptions>() ?? new();

        string connectionString = configuration.GetConnectionString(databaseOptions.ConnectionStringName)
            ?? throw new InvalidOperationException($"No existe la cadena de conexion '{databaseOptions.ConnectionStringName}'.");

        services.AddDbContext<DynamicPromotionsDbContext>(options =>
        {
            string migrationsAssembly = Assembly.GetEntryAssembly()?.GetName().Name
                ?? typeof(DynamicPromotionsModuleStartup).Assembly.GetName().Name!;

            options.UseMySql(
                connectionString,
                new MariaDbServerVersion(Version.Parse(databaseOptions.MariaDbVersion)),
                mySqlOptions => mySqlOptions.MigrationsAssembly(migrationsAssembly));
        });

        services.AddScoped<IPromotionService, PromotionService>();
        services.AddScoped<IPromotionAudienceBuilder, PromotionAudienceBuilder>();
        services.AddHttpClient<IPromotionPushSender, FirebasePromotionPushSender>();
        services.AddHostedService<PromotionDispatchWorker>();

        mvcBuilder.AddApplicationPart(typeof(BusinessPromotionsController).Assembly);
    }
}
