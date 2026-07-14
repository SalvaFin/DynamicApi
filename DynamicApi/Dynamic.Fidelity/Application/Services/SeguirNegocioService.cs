using Dynamic.Fidelity.Application.Common;
using Dynamic.Fidelity.Application.Contracts.Services;
using Dynamic.Fidelity.Application.DTOs.Responses;

namespace Dynamic.Fidelity.Application.Services;

public class SeguirNegocioService : ISeguirNegocioService
{
    private readonly INegocioAudienciaService _negocioAudienciaService;

    public SeguirNegocioService(
        INegocioAudienciaService negocioAudienciaService)
    {
        _negocioAudienciaService = negocioAudienciaService;
    }

    public async Task<ServiceResult<SeguirNegocioResponse>> SeguirAsync(
        Guid negocioId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ServiceResult<FormarParteNegocioResponse> result =
            await _negocioAudienciaService.FormarParteAsync(negocioId, userId, cancellationToken);
        if (!result.Succeeded || result.Data is null)
        {
            return ServiceResult<SeguirNegocioResponse>.Failure(
                result.ErrorCode ?? "validation_error",
                result.ErrorMessage ?? "No se ha podido formar parte del negocio.");
        }

        return ServiceResult<SeguirNegocioResponse>.Success(new SeguirNegocioResponse
        {
            NegocioId = negocioId,
            VinculacionId = result.Data.AudienciaId,
            YaEstabaVinculado = result.Data.YaFormabaParte,
            VinculadoAhora = result.Data.FormadoAhora,
            BonoBienvenidaRecibido = result.Data.BonoBienvenidaRecibido
        });
    }

    public async Task<ServiceResult> DejarDeSeguirAsync(
        Guid negocioId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _negocioAudienciaService.DejarDeFormarParteAsync(negocioId, userId, cancellationToken);
    }
}
