namespace Dynamic.Users.Application.DTOs.Responses;

public class RegisterStartResponse
{
    public bool AlreadyExists { get; set; }
    public bool PendingRegistrationCreated { get; set; }
    public bool NotificationSent { get; set; }
    public bool ShouldRedirectToLogin { get; set; }
    public string DeliveryChannel { get; set; } = string.Empty;
    public string NextAction { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Contact { get; set; } = string.Empty;
    public string? UserName { get; set; }
}
