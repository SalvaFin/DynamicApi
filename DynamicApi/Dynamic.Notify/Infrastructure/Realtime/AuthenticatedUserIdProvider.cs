using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace Dynamic.Notify.Infrastructure.Realtime;

public class AuthenticatedUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        string? claim = connection.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? connection.User?.FindFirstValue("sub");

        // SignalR compara el identificador de usuario como texto. Normalizar el GUID
        // evita perder eventos cuando el JWT lo contiene con mayusculas o con otro formato.
        return Guid.TryParse(claim, out Guid userId)
            ? userId.ToString("D")
            : claim;
    }
}
