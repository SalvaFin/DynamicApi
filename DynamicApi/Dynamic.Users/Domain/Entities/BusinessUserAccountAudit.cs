namespace Dynamic.Users.Domain.Entities;

public sealed class BusinessUserAccountAudit
{
    public Guid Id { get; set; }
    public Guid NegocioId { get; set; }
    public Guid UserId { get; set; }
    public Guid ModifiedByUserId { get; set; }
    public string ChangesJson { get; set; } = "{}";
    public DateTime CreatedAtUtc { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
