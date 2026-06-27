namespace Dynamic.Notify.Application.Models;

public class UserAppEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public object? Payload { get; set; }
}
