namespace Dynamic.Users.Application.DTOs.Responses;

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAtUtc { get; set; }
    public DateTime RefreshTokenExpiresAtUtc { get; set; }
    public UserSummaryResponse User { get; set; } = new();
    public UserSessionResponse CurrentSession { get; set; } = new();
}
