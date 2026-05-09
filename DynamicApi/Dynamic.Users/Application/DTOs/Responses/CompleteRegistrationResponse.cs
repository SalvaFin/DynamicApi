namespace Dynamic.Users.Application.DTOs.Responses;

public class CompleteRegistrationResponse
{
    public bool Completed { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Contact { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public bool LoggedIn { get; set; }
    public AuthResponse? Auth { get; set; }
}
