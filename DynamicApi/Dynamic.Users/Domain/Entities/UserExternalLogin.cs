using Dynamic.Users.Domain.Enums;

namespace Dynamic.Users.Domain.Entities;

public class UserExternalLogin
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public UserAccount User { get; set; } = null!;
    public ExternalAuthProvider Provider { get; set; }
    public string ProviderSubject { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastLoginAtUtc { get; set; }
}
