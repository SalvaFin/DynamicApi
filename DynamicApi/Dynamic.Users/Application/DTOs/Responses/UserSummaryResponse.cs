using Dynamic.Users.Domain.Enums;

namespace Dynamic.Users.Application.DTOs.Responses;

public class UserSummaryResponse
{
    public Guid Id { get; set; }
    public string? UserCode { get; set; }
    public string? Email { get; set; }
    public string UserName { get; set; } = string.Empty;
    public bool RequiresPasswordChange { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? DisplayName { get; set; }
    public string? PhoneNumber { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool PhoneNumberConfirmed { get; set; }
    public bool RegistrationCompleted { get; set; }
    public int? AgeAtRegistration { get; set; }
    public DateTime? BirthDate { get; set; }
    public UserGender Gender { get; set; }
    public UserRole Role { get; set; }
    public UserStatus Status { get; set; }
    public string? Language { get; set; }
    public string? TimeZone { get; set; }
    public string? CountryCode { get; set; }
    public string? Region { get; set; }
    public string? City { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
}
