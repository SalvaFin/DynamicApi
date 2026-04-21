using System.ComponentModel.DataAnnotations;

namespace Dynamic.Fidelity.Application.DTOs.Requests;

public class ValidatePointsEarnOperationRequest
{
    [Required]
    [RegularExpression(@"^\d{4}$")]
    public string MasterPin { get; set; } = string.Empty;
}
