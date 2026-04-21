using System.ComponentModel.DataAnnotations;

namespace Dynamic.Fidelity.Application.DTOs.Requests;

public class InitiatePointsEarnRequest
{
    public decimal AmountEuros { get; set; }
}
