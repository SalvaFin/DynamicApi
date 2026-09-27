namespace Dynamic.Users.Application.DTOs.Responses;

public sealed class BusinessUserAccountAuditResponse
{
    public Guid Id { get; set; }
    public Guid NegocioId { get; set; }
    public Guid UserId { get; set; }
    public Guid ModifiedByUserId { get; set; }
    public string? ModifiedByUserName { get; set; }
    public IReadOnlyDictionary<string, object?> Changes { get; set; } = new Dictionary<string, object?>();
    public DateTime CreatedAtUtc { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
