using System.Reflection;
using Dynamic.Users.Application.Contracts.Repositories;
using Dynamic.Users.Application.Contracts.Services;
using Dynamic.Users.Application.Options;
using Dynamic.Users.Application.Services;
using Dynamic.Users.Controllers;
using Dynamic.Users.Domain.Entities;
using Dynamic.Users.Infrastructure.Persistence;
using Dynamic.Users.Infrastructure.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dynamic.Users.DependencyInjection;

public class DynamicUsersModuleStartup
{
    public void RegisterModule(IServiceCollection services, IConfiguration configuration, IMvcBuilder mvcBuilder)
    {
        services.AddOptions<DynamicUsersDatabaseOptions>()
            .Bind(configuration.GetSection(DynamicUsersDatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<UserRegistrationOptions>()
            .Bind(configuration.GetSection(UserRegistrationOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        DynamicUsersDatabaseOptions databaseOptions = configuration
            .GetSection(DynamicUsersDatabaseOptions.SectionName)
            .Get<DynamicUsersDatabaseOptions>() ?? new();

        string connectionString = configuration.GetConnectionString(databaseOptions.ConnectionStringName)
            ?? throw new InvalidOperationException($"No existe la cadena de conexión '{databaseOptions.ConnectionStringName}'.");

        services.AddDbContext<DynamicUsersDbContext>(options =>
        {
            string migrationsAssembly = Assembly.GetEntryAssembly()?.GetName().Name
                ?? typeof(DynamicUsersModuleStartup).Assembly.GetName().Name!;

            options.UseMySql(
                connectionString,
                new MariaDbServerVersion(Version.Parse(databaseOptions.MariaDbVersion)),
                mySqlOptions => mySqlOptions.MigrationsAssembly(migrationsAssembly));
        });

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserDeviceRepository, UserDeviceRepository>();
        services.AddScoped<IUserSessionRepository, UserSessionRepository>();
        services.AddScoped<IUserAuthEventRepository, UserAuthEventRepository>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IBusinessUserProvisioningService, BusinessUserProvisioningService>();
        services.AddScoped<IPasswordHasher<UserAccount>, PasswordHasher<UserAccount>>();
        services.AddHttpContextAccessor();

        mvcBuilder.AddApplicationPart(typeof(UsersAuthController).Assembly);
    }
}
