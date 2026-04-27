using Dynamic.Negocios.Application.Common;
using Dynamic.Negocios.Application.DTOs.Requests;
using Dynamic.Negocios.Application.DTOs.Responses;

namespace Dynamic.Negocios.Application.Contracts.Services;

public interface INegocioService
{
    Task<ServiceResult<IReadOnlyCollection<NegocioResponse>>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<NegocioResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ServiceResult<NegocioResponse>> GetBySlugAsync(string slugPortal, CancellationToken cancellationToken = default);
    Task<ServiceResult<NegocioResponse>> CreateAsync(CrearNegocioRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<NegocioResponse>> UpdateAsync(Guid id, ActualizarNegocioRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
