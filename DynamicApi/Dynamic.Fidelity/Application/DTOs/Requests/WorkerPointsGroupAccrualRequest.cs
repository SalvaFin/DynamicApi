using System.ComponentModel.DataAnnotations;

namespace Dynamic.Fidelity.Application.DTOs.Requests;

public class WorkerPointsGroupAccrualRequest
{
    public Guid WorkerUserId { get; set; }
    public Guid IdempotencyKey { get; set; }
    public decimal AmountEuros { get; set; }
    public List<Guid> RecipientUserIds { get; set; } = [];

    [MaxLength(512)]
    public string? Reason { get; set; }
}
