namespace Dynamic.Fidelity.Domain.Entities;

public class QrCampaign
{
    public Guid Id { get; set; }
    public Guid NegocioId { get; set; }
    public Guid? WelcomeTicketTemplateId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? LandingPath { get; set; }
    public bool Activa { get; set; } = true;
    public bool Visible { get; set; } = true;
    public bool UnSoloUsoPorUsuario { get; set; } = true;
    public bool Expira { get; set; }
    public DateTime? AvailableFromUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
