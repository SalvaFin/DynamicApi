using System.ComponentModel.DataAnnotations;

namespace Dynamic.Reports.Application.DTOs.Requests;

public sealed class AddReportMessageRequest
{
    [Required, StringLength(5000, MinimumLength = 2)]
    public string Message { get; set; } = string.Empty;
}
