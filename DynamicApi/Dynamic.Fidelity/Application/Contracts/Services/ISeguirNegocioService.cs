using Dynamic.Fidelity.Application.Common;
using Dynamic.Fidelity.Application.DTOs.Responses;

namespace Dynamic.Fidelity.Application.Contracts.Services;

public interface ISeguirNegocioService
{
    Task<ServiceResult<SeguirNegocioResponse>> SeguirAsync(
        Guid negocioId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> DejarDeSeguirAsync(
        Guid negocioId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
