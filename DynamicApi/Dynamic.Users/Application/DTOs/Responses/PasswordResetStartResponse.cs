namespace Dynamic.Users.Application.DTOs.Responses;

public class PasswordResetStartResponse
{
    public bool RequestAccepted { get; set; }
    public string Message { get; set; } = string.Empty;
}
