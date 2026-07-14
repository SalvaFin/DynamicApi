using System.Reflection;
using Dynamic.Fidelity.Application.Contracts.Repositories;
using Dynamic.Fidelity.Application.Contracts.Services;
using Dynamic.Fidelity.Application.Options;
using Dynamic.Fidelity.Application.Services;
using Dynamic.Fidelity.Controllers;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Dynamic.Fidelity.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dynamic.Fidelity.DependencyInjection;

public class DynamicFidelityModuleStartup
{
    public void RegisterModule(IServiceCollection services, IConfiguration configuration, IMvcBuilder mvcBuilder)
    {
        services.AddOptions<DynamicFidelityDatabaseOptions>()
            .Bind(configuration.GetSection(DynamicFidelityDatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<FidelityQrOptions>()
            .Bind(configuration.GetSection(FidelityQrOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        DynamicFidelityDatabaseOptions databaseOptions = configuration
            .GetSection(DynamicFidelityDatabaseOptions.SectionName)
            .Get<DynamicFidelityDatabaseOptions>() ?? new();

        string connectionString = configuration.GetConnectionString(databaseOptions.ConnectionStringName)
            ?? throw new InvalidOperationException($"No existe la cadena de conexión '{databaseOptions.ConnectionStringName}'.");

        services.AddDbContext<DynamicFidelityDbContext>(options =>
        {
            string migrationsAssembly = Assembly.GetEntryAssembly()?.GetName().Name
                ?? typeof(DynamicFidelityModuleStartup).Assembly.GetName().Name!;

            options.UseMySql(
                connectionString,
                new MariaDbServerVersion(Version.Parse(databaseOptions.MariaDbVersion)),
                mySqlOptions => mySqlOptions.MigrationsAssembly(migrationsAssembly));
        });

        services.AddScoped<IPointsRepository, PointsRepository>();
        services.AddScoped<IPointsTransactionRepository, PointsTransactionRepository>();
        services.AddScoped<IPointsOperationRepository, PointsOperationRepository>();
        services.AddScoped<IPointsOperationAttemptRepository, PointsOperationAttemptRepository>();
        services.AddScoped<IUserCodeDirectoryRepository, UserCodeDirectoryRepository>();
        services.AddScoped<IPointsService, PointsService>();
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<ITicketService, TicketService>();
        services.AddScoped<ITicketEventPublisher, TicketEventPublisher>();
        services.AddScoped<IQrCampaignRepository, QrCampaignRepository>();
        services.AddScoped<IPendingTicketAssignmentRepository, PendingTicketAssignmentRepository>();
        services.AddScoped<IRegistrationRewardService, RegistrationRewardService>();
        services.AddScoped<INegocioAudienciaService, NegocioAudienciaService>();
        services.AddScoped<ISeguirNegocioService, SeguirNegocioService>();
        services.AddScoped<ITicketQrService, TicketQrService>();
        services.AddScoped<IBusinessQrService, BusinessQrService>();
        services.AddScoped<IUserCodeDirectoryService, UserCodeDirectoryService>();

        mvcBuilder.AddApplicationPart(typeof(TicketQrController).Assembly);
        mvcBuilder.AddApplicationPart(typeof(PointsController).Assembly);
    }
}
