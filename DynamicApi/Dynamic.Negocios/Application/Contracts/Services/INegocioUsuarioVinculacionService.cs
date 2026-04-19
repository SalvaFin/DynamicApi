using Dynamic.Negocios.Application.Common;
using Dynamic.Negocios.Application.DTOs.Requests;
using Dynamic.Negocios.Application.DTOs.Responses;

namespace Dynamic.Negocios.Application.Contracts.Services;

public interface INegocioUsuarioVinculacionService
{
    Task<ServiceResult<IReadOnlyCollection<NegocioVinculadoResponse>>> GetNegociosByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ServiceResult<NegocioUsuarioVinculacionResponse>> LinkUserAsync(Guid negocioId, Guid userId, VincularUsuarioNegocioRequest request, Guid? linkedByUserId = null, CancellationToken cancellationToken = default);
    Task<ServiceResult> UnlinkUserAsync(Guid negocioId, Guid userId, Guid? unlinkedByUserId = null, CancellationToken cancellationToken = default);
}
