using Dynamic.Negocios.Application.Common;
using Microsoft.AspNetCore.Http;

namespace Dynamic.Negocios.Application.Contracts.Services;

public interface INegocioMediaStorageService
{
    Task<ServiceResult<string>> SaveImageAsync(
        Guid negocioId,
        string imageSlot,
        IFormFile file,
        CancellationToken cancellationToken = default);
}
