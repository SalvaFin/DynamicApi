using System.Globalization;
using System.Net;
using System.Text;
using Dynamic.Fidelity.Application.DTOs.Responses;
using Dynamic.Notify.Application.Models;
using Dynamic.Promotions.Application.Options;
using Dynamic.Promotions.Domain.Entities;

namespace Dynamic.Promotions.Application.Services;

public static class PromotionEmailTemplate
{
    public static EmailMessage Build(
        PromotionEmailDelivery delivery,
        TicketResponse? ticket,
        PromotionEmailOptions options)
    {
        PromotionCampaign campaign = delivery.Campaign;
        string businessName = campaign.NegocioNombreSnapshot;
        string promotionName = ticket?.Nombre ?? campaign.TicketNombreSnapshot;
        string description = ticket?.Descripcion ?? campaign.TicketDescripcionSnapshot ??
            $"Has recibido una nueva promoción de {businessName}.";
        string greeting = string.IsNullOrWhiteSpace(delivery.RecipientName)
            ? "¡Hola!"
            : $"¡Hola, {delivery.RecipientName.Trim()}!";
        string ticketsUrl = CombineUrl(options.AppBaseUrl, "/portal/tickets");
        string unsubscribeUrl = CombineUrl(
            options.PublicApiBaseUrl,
            $"/api/promotions/email/unsubscribe?token={delivery.UnsubscribeToken:D}");
        string? mapsUrl = BuildMapsUrl(campaign);
        string location = campaign.NegocioAddressSnapshot ?? "Consulta la ubicación en la app";

        string encodedLogo = Encode(BuildPublicUrl(options.PublicApiBaseUrl, campaign.NegocioLogoUrlSnapshot));
        string logo = string.IsNullOrWhiteSpace(encodedLogo)
            ? "<div style=\"font-size:28px;font-weight:800;color:#c77dff\">DYNAMIC</div>"
            : $"<img src=\"{encodedLogo}\" alt=\"{Encode(businessName)}\" width=\"72\" style=\"display:block;border:0;border-radius:16px;max-height:72px;object-fit:contain\">";
        string mapsBlock = mapsUrl is null
            ? $"<div style=\"color:#c9bdd3;font-size:14px;line-height:21px\">📍 {Encode(location)}</div>"
            : $"<a href=\"{Encode(mapsUrl)}\" style=\"color:#d7a0ff;font-size:14px;line-height:21px;text-decoration:underline\">📍 {Encode(location)} · Ver en Google Maps</a>";

        string html = $$"""
            <!doctype html>
            <html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width"></head>
            <body style="margin:0;background:#08060c;font-family:Arial,Helvetica,sans-serif;color:#f8f1ff">
              <div style="display:none;max-height:0;overflow:hidden">{{Encode(promotionName)}} de {{Encode(businessName)}}</div>
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#08060c"><tr><td align="center" style="padding:32px 12px">
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:600px;background:#120d18;border:1px solid #4b2365;border-radius:24px;overflow:hidden">
                  <tr><td style="height:6px;background:#9d4edd"></td></tr>
                  <tr><td style="padding:32px 36px 12px">{{logo}}</td></tr>
                  <tr><td style="padding:12px 36px 0;color:#c9bdd3;font-size:16px">{{Encode(greeting)}}</td></tr>
                  <tr><td style="padding:10px 36px 0;font-size:30px;line-height:36px;font-weight:800">Tienes una nueva promoción</td></tr>
                  <tr><td style="padding:26px 36px">
                    <div style="background:#1c1226;border:1px solid #6d3293;border-radius:20px;padding:26px">
                      <div style="color:#d7a0ff;text-transform:uppercase;letter-spacing:3px;font-size:11px;font-weight:700">{{Encode(businessName)}}</div>
                      <div style="padding-top:10px;font-size:25px;line-height:31px;font-weight:800">{{Encode(promotionName)}}</div>
                      <div style="padding-top:12px;color:#e4d9eb;font-size:16px;line-height:25px">{{Encode(description)}}</div>
                      <div style="padding-top:20px">{{mapsBlock}}</div>
                      <div style="padding-top:24px"><a href="{{Encode(ticketsUrl)}}" style="display:inline-block;background:#a855f7;color:#fff;text-decoration:none;font-weight:800;padding:14px 24px;border-radius:12px">Ver mi promoción</a></div>
                    </div>
                  </td></tr>
                  <tr><td style="padding:0 36px 34px;color:#8f8299;font-size:12px;line-height:19px">
                    Recibes este correo porque aceptaste comunicaciones comerciales de Dynamic. Puedes
                    <a href="{{Encode(unsubscribeUrl)}}" style="color:#c891f2">darte de baja aquí</a>.
                    {{Encode(options.CompanyName)}}{{BuildAddressSuffix(options.CompanyAddress)}}
                  </td></tr>
                </table>
              </td></tr></table>
            </body></html>
            """;

        StringBuilder text = new();
        text.AppendLine(greeting).AppendLine()
            .AppendLine($"Has recibido una promoción de {businessName}:")
            .AppendLine(promotionName).AppendLine(description).AppendLine();
        if (mapsUrl is not null)
        {
            text.AppendLine($"Ubicación: {location}").AppendLine(mapsUrl).AppendLine();
        }
        text.AppendLine($"Ver mi promoción: {ticketsUrl}").AppendLine()
            .AppendLine($"Darte de baja: {unsubscribeUrl}");

        return new EmailMessage
        {
            ToEmail = delivery.Email,
            ToName = delivery.RecipientName,
            Subject = $"{businessName}: {promotionName}",
            HtmlBody = html,
            TextBody = text.ToString(),
            ListUnsubscribeUrl = unsubscribeUrl
        };
    }

    private static string? BuildMapsUrl(PromotionCampaign campaign)
    {
        string query;
        if (campaign.NegocioLatitudeSnapshot.HasValue && campaign.NegocioLongitudeSnapshot.HasValue)
        {
            query = $"{campaign.NegocioLatitudeSnapshot.Value.ToString(CultureInfo.InvariantCulture)},{campaign.NegocioLongitudeSnapshot.Value.ToString(CultureInfo.InvariantCulture)}";
        }
        else if (!string.IsNullOrWhiteSpace(campaign.NegocioAddressSnapshot))
        {
            query = campaign.NegocioAddressSnapshot;
        }
        else
        {
            return null;
        }

        return $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(query)}";
    }

    private static string CombineUrl(string baseUrl, string path) => $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}";

    private static string? BuildPublicUrl(string baseUrl, string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        string normalizedUrl = url.Trim();
        if (Uri.TryCreate(normalizedUrl, UriKind.Absolute, out Uri? absoluteUrl) &&
            (string.Equals(absoluteUrl.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(absoluteUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            return absoluteUrl.ToString();
        }

        return CombineUrl(baseUrl, normalizedUrl);
    }

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
    private static string BuildAddressSuffix(string? address) => string.IsNullOrWhiteSpace(address) ? string.Empty : $" · {Encode(address)}";
}
