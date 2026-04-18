using Dynamic.Notify.Application.Contracts;
using Dynamic.Notify.Application.Options;
using Dynamic.Notify.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dynamic.Notify.DependencyInjection;

public class DynamicNotifyModuleStartup
{
    public void RegisterModule(IServiceCollection services, IConfiguration configuration, IMvcBuilder mvcBuilder)
    {
        services.AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection(SmtpOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IEmailNotificationService, SmtpEmailNotificationService>();
    }
}
