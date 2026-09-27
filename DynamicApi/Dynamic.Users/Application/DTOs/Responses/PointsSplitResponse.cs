namespace Dynamic.Users.Application.DTOs.Responses;

public class SplitRecipientResponse
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int PointsAmount { get; set; }
}

public class PointsSplitResponse
{
    public Guid Id { get; set; }
    public Guid SourceTransactionId { get; set; }
    public Guid NegocioId { get; set; }
    public int OriginalPoints { get; set; }
    public int OwnerShare { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public List<SplitRecipientResponse> Recipients { get; set; } = [];
}

public class PointsSplitStatusResponse
{
    public Guid SourceTransactionId { get; set; }
    public Guid NegocioId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public int OriginalPoints { get; set; }
    public int CurrentBalance { get; set; }
    public bool Eligible { get; set; }
    public string? IneligibleReason { get; set; }
    public PointsSplitResponse? Split { get; set; }
}
