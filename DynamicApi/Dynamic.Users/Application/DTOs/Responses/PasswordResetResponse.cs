namespace Dynamic.Users.Application.DTOs.Responses;

public class PasswordResetResponse
{
    public bool PasswordChanged { get; set; }
    public string Message { get; set; } = string.Empty;
}
