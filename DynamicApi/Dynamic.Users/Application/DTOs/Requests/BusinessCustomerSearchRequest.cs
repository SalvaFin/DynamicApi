using System.ComponentModel.DataAnnotations;

namespace Dynamic.Users.Application.DTOs.Requests;

public class BusinessCustomerSearchRequest
{
    [Required]
    [MaxLength(256)]
    public string Contact { get; set; } = string.Empty;
}
