using System.ComponentModel.DataAnnotations;

namespace Dynamic.Fidelity.Application.DTOs.Requests;

public class WorkerPointsAccrualRequest
{
    public Guid TrabajadorId { get; set; }
    public Guid UserId { get; set; }
    public decimal DineroGastado { get; set; }

    [MaxLength(512)]
    public string? Reason { get; set; }

    [MaxLength(256)]
    public string? Reference { get; set; }
}
