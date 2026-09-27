using System.Net;
using Dynamic.Notify.Application.Models;
using Dynamic.Users.Domain.Entities;

namespace Dynamic.Users.Application.Services;

internal static class BusinessAccountEmailTemplate
{
    public static EmailMessage Build(UserAccount user, string businessName, bool isOwner, bool existingAccount, string panelUrl)
    {
        string role = isOwner ? "propietario" : "trabajador";
        string greetingName = user.DisplayName ?? user.UserName;
        string notice = existingAccount
            ? $"Tu cuenta de Dynamic se ha vinculado como {role} de {businessName}."
            : $"Se ha creado tu cuenta de {role} para {businessName} en Dynamic.";
        string accessNote = existingAccount
            ? "Si ya tienes una sesión abierta, ciérrala y vuelve a entrar para actualizar tus permisos."
            : "Accede con tus credenciales. Si no conoces la contraseña, puedes restablecerla desde la página de acceso.";

        return new EmailMessage
        {
            ToEmail = user.Email!,
            ToName = greetingName,
            Subject = existingAccount ? $"Nuevo acceso de {role} en Dynamic" : $"Tu cuenta de {role} en Dynamic",
            HtmlBody = $$"""
                <!doctype html>
                <html lang="es"><head><meta charset="utf-8"></head>
                <body style="font-family:Arial,sans-serif;color:#241b2b;line-height:1.5">
                  <h1>Tu acceso a Dynamic</h1>
                  <p>Hola, {{WebUtility.HtmlEncode(greetingName)}}:</p>
                  <p>{{WebUtility.HtmlEncode(notice)}}</p>
                  <p><a href="{{WebUtility.HtmlEncode(panelUrl)}}">Entrar al panel</a></p>
                  <p>{{WebUtility.HtmlEncode(accessNote)}}</p>
                </body></html>
                """,
            TextBody = $"Hola, {greetingName}:\n\n{notice}\n\nEntrar al panel: {panelUrl}\n\n{accessNote}"
        };
    }
}
