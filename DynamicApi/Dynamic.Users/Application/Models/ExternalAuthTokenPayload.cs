using Dynamic.Users.Domain.Enums;

namespace Dynamic.Users.Application.Models;

public class ExternalAuthTokenPayload
{
    public ExternalAuthProvider Provider { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool EmailVerified { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? HostedDomain { get; set; }
    public string? Nonce { get; set; }
}
