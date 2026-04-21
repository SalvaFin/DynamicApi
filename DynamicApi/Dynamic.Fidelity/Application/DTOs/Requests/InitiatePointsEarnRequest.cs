using System.ComponentModel.DataAnnotations;

namespace Dynamic.Fidelity.Application.DTOs.Requests;

public class InitiatePointsEarnRequest
{
    [Range(typeof(decimal), "0.01", "999999999")]
    public decimal AmountEuros { get; set; }
}
