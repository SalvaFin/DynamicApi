using Dynamic.Negocios.Application.DTOs.Responses;

namespace Dynamic.Users.Application.DTOs.Responses;

public class BusinessUserAccountResponse
{
    public Guid NegocioId { get; set; }
    public bool IsOwner { get; set; }
    public UserSummaryResponse User { get; set; } = new();
    public NegocioUsuarioVinculacionResponse Vinculacion { get; set; } = new();
}
