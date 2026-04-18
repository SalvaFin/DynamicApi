using System.Net;
using System.Net.Mail;
using Dynamic.Notify.Application.Contracts;
using Dynamic.Notify.Application.Models;
using Dynamic.Notify.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

        using MailMessage mailMessage = new()
        {
            From = new MailAddress(_smtpOptions.FromEmail, _smtpOptions.FromName),
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true
        };

        mailMessage.To.Add(new MailAddress(message.ToEmail, message.ToName ?? message.ToEmail));

        if (!string.IsNullOrWhiteSpace(message.TextBody))
        {
            mailMessage.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.TextBody, null, "text/plain"));
        }

        using SmtpClient smtpClient = new(_smtpOptions.Host, _smtpOptions.Port)
        {
            EnableSsl = _smtpOptions.UseSsl,
            Credentials = new NetworkCredential(_smtpOptions.UserName, _smtpOptions.Password)
        };

        cancellationToken.ThrowIfCancellationRequested();
        await smtpClient.SendMailAsync(mailMessage, cancellationToken);
    }
}
