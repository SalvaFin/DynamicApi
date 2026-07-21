using Dynamic.Notify.Application.Contracts;
using Dynamic.Notify.Application.Models;
using Dynamic.Notify.Application.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Dynamic.Notify.Infrastructure.Services;

public class SmtpEmailNotificationService : IEmailNotificationService
{
    private readonly SmtpOptions _smtpOptions;
    private readonly ILogger<SmtpEmailNotificationService> _logger;

    public SmtpEmailNotificationService(
        IOptions<SmtpOptions> smtpOptions,
        ILogger<SmtpEmailNotificationService> logger)
    {
        _smtpOptions = smtpOptions.Value;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (!_smtpOptions.Enabled)
        {
            _logger.LogInformation("SMTP deshabilitado. Correo omitido para {ToEmail}", message.ToEmail);
            return;
        }

        MimeMessage mailMessage = new();
        mailMessage.From.Add(new MailboxAddress(_smtpOptions.FromName, _smtpOptions.FromEmail));
        mailMessage.To.Add(new MailboxAddress(message.ToName ?? message.ToEmail, message.ToEmail));
        mailMessage.Subject = message.Subject;
        if (!string.IsNullOrWhiteSpace(message.ListUnsubscribeUrl))
        {
            mailMessage.Headers["List-Unsubscribe"] = $"<{message.ListUnsubscribeUrl}>";
            mailMessage.Headers["List-Unsubscribe-Post"] = "List-Unsubscribe=One-Click";
        }

        BodyBuilder bodyBuilder = new()
        {
            HtmlBody = message.HtmlBody
        };
        if (!string.IsNullOrWhiteSpace(message.TextBody))
        {
            bodyBuilder.TextBody = message.TextBody;
        }

        mailMessage.Body = bodyBuilder.ToMessageBody();

        SecureSocketOptions secureSocketOptions = GetSecureSocketOptions();

        using SmtpClient smtpClient = new();
        await smtpClient.ConnectAsync(_smtpOptions.Host, _smtpOptions.Port, secureSocketOptions, cancellationToken);
        await smtpClient.AuthenticateAsync(_smtpOptions.UserName, _smtpOptions.Password, cancellationToken);
        await smtpClient.SendAsync(mailMessage, cancellationToken);
        await smtpClient.DisconnectAsync(true, cancellationToken);
    }

    private SecureSocketOptions GetSecureSocketOptions()
    {
        if (!_smtpOptions.UseSsl)
        {
            return SecureSocketOptions.None;
        }

        return _smtpOptions.Port == 465
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTls;
    }
}
