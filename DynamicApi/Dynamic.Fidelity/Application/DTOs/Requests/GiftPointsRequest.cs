using System.ComponentModel.DataAnnotations;

namespace Dynamic.Fidelity.Application.DTOs.Requests;

public class GiftPointsRequest
{
    public Guid? RecipientUserId { get; set; }

    [MaxLength(32)]
    public string? RecipientUserCode { get; set; }

    [Range(1, int.MaxValue)]
    public int Amount { get; set; }

    [MaxLength(512)]
    public string? Reason { get; set; }

    [MaxLength(256)]
    public string? Reference { get; set; }
}
