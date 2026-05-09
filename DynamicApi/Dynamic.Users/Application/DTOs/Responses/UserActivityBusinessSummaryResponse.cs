namespace Dynamic.Users.Application.DTOs.Responses;

public class UserActivityBusinessSummaryResponse
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? IconoUrl { get; set; }
}
