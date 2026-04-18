using Dynamic.Users.Domain.Enums;

namespace Dynamic.Users.Domain.Entities;

public class UserAuthEvent
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public AuthEventType EventType { get; set; }
    public string? Identity { get; set; }
    public bool Succeeded { get; set; }
    public string? FailureReason { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? ClientSummary { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public UserAccount? User { get; set; }
}
