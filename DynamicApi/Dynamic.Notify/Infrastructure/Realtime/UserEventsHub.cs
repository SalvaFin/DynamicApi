using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Dynamic.Notify.Infrastructure.Realtime;

[Authorize]
public class UserEventsHub : Hub
{
}
