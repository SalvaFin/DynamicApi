using Dynamic.Fidelity.Application.Common;
using Dynamic.Fidelity.Application.DTOs.Responses;

namespace Dynamic.Fidelity.Application.Contracts.Services;

public interface IBusinessQrService
{
    Task<ServiceResult<BusinessQrResponse>> GenerateAsync(
        Guid negocioId,
        Guid requesterUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default);
}
