using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace Dynamic.Notify.Infrastructure.Realtime;

public class AuthenticatedUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
        => connection.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? connection.User?.FindFirstValue("sub");
}
