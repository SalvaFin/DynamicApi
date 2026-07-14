using Dynamic.Users.Domain.Enums;

namespace Dynamic.Users.Application.DTOs.Responses;

public class BusinessCustomerLookupResponse
{
    public Guid UserId { get; set; }
    public string? UserCode { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? DisplayName { get; set; }
    public UserStatus Status { get; set; }
    public bool RegistrationCompleted { get; set; }
    public string MatchType { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
