using System.ComponentModel.DataAnnotations;

namespace Dynamic.Notify.Application.Options;

public class SmtpOptions
{
    public const string SectionName = "Notify:Smtp";

    public bool Enabled { get; set; } = true;

    [Required]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; } = 587;

    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public bool UseSsl { get; set; } = true;

    [Required]
    public string FromEmail { get; set; } = string.Empty;

    public string FromName { get; set; } = "Dynamic Notify";
}
