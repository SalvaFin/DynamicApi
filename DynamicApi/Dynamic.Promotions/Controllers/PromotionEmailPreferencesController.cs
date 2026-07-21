using System.Net;
using Dynamic.Promotions.Infrastructure.Persistence;
using Dynamic.Users.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dynamic.Promotions.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/promotions/email")]
public class PromotionEmailPreferencesController : ControllerBase
{
    private readonly DynamicPromotionsDbContext _promotionsDbContext;
    private readonly DynamicUsersDbContext _usersDbContext;

    public PromotionEmailPreferencesController(
        DynamicPromotionsDbContext promotionsDbContext,
        DynamicUsersDbContext usersDbContext)
    {
        _promotionsDbContext = promotionsDbContext;
        _usersDbContext = usersDbContext;
    }

    [HttpGet("unsubscribe")]
    public async Task<ContentResult> ConfirmUnsubscribe([FromQuery] Guid token, CancellationToken cancellationToken)
    {
        var delivery = await _promotionsDbContext.EmailDeliveries.AsNoTracking()
            .Where(item => item.UnsubscribeToken == token)
            .Select(item => new { item.Email })
            .FirstOrDefaultAsync(cancellationToken);
        if (delivery is null)
        {
            return Html("Enlace no válido", "Este enlace de baja no es válido o ya no está disponible.", null, 404);
        }

        string action = $"/api/promotions/email/unsubscribe?token={token:D}";
        return Html(
            "Dejar de recibir promociones",
            $"Vas a desactivar los correos promocionales enviados a {WebUtility.HtmlEncode(delivery.Email)}.",
            action,
            200);
    }

    [HttpPost("unsubscribe")]
    [IgnoreAntiforgeryToken]
    public async Task<ContentResult> Unsubscribe([FromQuery] Guid token, CancellationToken cancellationToken)
    {
        var delivery = await _promotionsDbContext.EmailDeliveries.AsNoTracking()
            .Where(item => item.UnsubscribeToken == token)
            .Select(item => new { item.UserId })
            .FirstOrDefaultAsync(cancellationToken);
        if (delivery is null)
        {
            return Html("Enlace no válido", "No se ha encontrado la suscripción.", null, 404);
        }

        var user = await _usersDbContext.Users.FirstOrDefaultAsync(item => item.Id == delivery.UserId, cancellationToken);
        if (user is not null && user.MarketingAccepted)
        {
            user.MarketingAccepted = false;
            user.MarketingAcceptedAtUtc = null;
            user.UpdatedAtUtc = DateTime.UtcNow;
            await _usersDbContext.SaveChangesAsync(cancellationToken);
        }

        return Html("Baja completada", "Ya no recibirás correos promocionales de Dynamic.", null, 200);
    }

    private ContentResult Html(string title, string message, string? formAction, int statusCode)
    {
        string button = formAction is null ? string.Empty :
            $"<form method=\"post\" action=\"{WebUtility.HtmlEncode(formAction)}\"><button type=\"submit\">Confirmar baja</button></form>";
        string content = $$"""
            <!doctype html><html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width">
            <title>{{WebUtility.HtmlEncode(title)}}</title><style>
            body{margin:0;background:#08060c;color:#f8f1ff;font-family:Arial,sans-serif;display:grid;place-items:center;min-height:100vh}
            main{max-width:520px;margin:24px;padding:36px;background:#120d18;border:1px solid #5b2a78;border-radius:22px;text-align:center}
            h1{color:#d7a0ff}p{color:#d8cddd;line-height:1.6}button{margin-top:18px;background:#a855f7;color:white;border:0;border-radius:12px;padding:14px 22px;font-weight:700;cursor:pointer}
            </style></head><body><main><h1>{{WebUtility.HtmlEncode(title)}}</h1><p>{{message}}</p>{{button}}</main></body></html>
            """;
        return new ContentResult { Content = content, ContentType = "text/html; charset=utf-8", StatusCode = statusCode };
    }
}
