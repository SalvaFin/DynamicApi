namespace Dynamic.Fidelity.Application.DTOs.Responses;

public class UserPortalBusinessSummaryResponse
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? IconoUrl { get; set; }
}
