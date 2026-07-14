using System.ComponentModel.DataAnnotations;

namespace Dynamic.Users.Application.DTOs.Requests;

public class BusinessCustomerPointsAccrualRequest
{
    public Guid UserId { get; set; }
    public decimal AmountEuros { get; set; }

    [MaxLength(512)]
    public string? Reason { get; set; }

    [MaxLength(256)]
    public string? Reference { get; set; }
}
