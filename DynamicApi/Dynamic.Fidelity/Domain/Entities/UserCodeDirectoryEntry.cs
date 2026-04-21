namespace Dynamic.Fidelity.Domain.Entities;

public class UserCodeDirectoryEntry
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserCode { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
