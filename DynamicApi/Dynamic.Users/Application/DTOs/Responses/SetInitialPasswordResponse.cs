namespace Dynamic.Users.Application.DTOs.Responses;

public class SetInitialPasswordResponse
{
    public bool PasswordChanged { get; set; }
    public bool RequiresPasswordChange { get; set; }
    public string Message { get; set; } = string.Empty;
}
