using Dynamic.Negocios.Application.DTOs.Responses;

namespace Dynamic.Users.Application.DTOs.Responses;

public class BusinessCustomerRegistrationResponse
{
    public Guid NegocioId { get; set; }
    public bool Created { get; set; }
    public bool ExistingUser { get; set; }
    public bool LinkedNow { get; set; }
    public bool ReceivedWelcomeTicket { get; set; }
    public string Message { get; set; } = string.Empty;
    public UserSummaryResponse User { get; set; } = new();
    public NegocioUsuarioVinculacionResponse Vinculacion { get; set; } = new();
}
