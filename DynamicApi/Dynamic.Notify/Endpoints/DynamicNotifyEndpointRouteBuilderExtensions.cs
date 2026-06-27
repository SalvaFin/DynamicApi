using Dynamic.Notify.Infrastructure.Realtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Dynamic.Notify.Endpoints;

public static class DynamicNotifyEndpointRouteBuilderExtensions
{
    public const string UserEventsHubPath = "/hubs/user-events";

    public static IEndpointRouteBuilder MapDynamicNotifyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<UserEventsHub>(UserEventsHubPath);
        return endpoints;
    }
}
