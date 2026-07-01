using Dynamic.Fidelity.Application.Common;
using Dynamic.Fidelity.Application.Contracts.Services;
using Dynamic.Fidelity.Application.DTOs.Responses;
using Dynamic.Negocios.Application.Contracts.Repositories;
using Dynamic.Negocios.Application.Contracts.Services;
using Dynamic.Negocios.Application.DTOs.Requests;
using Dynamic.Negocios.Application.DTOs.Responses;
using Dynamic.Negocios.Domain.Entities;
using Dynamic.Negocios.Domain.Enums;

namespace Dynamic.Fidelity.Application.Services;

public class SeguirNegocioService : ISeguirNegocioService
{
    private readonly INegocioRepository _negocioRepository;
    private readonly INegocioUsuarioVinculacionRepository _vinculacionRepository;
    private readonly INegocioUsuarioVinculacionService _vinculacionService;
    private readonly IRegistrationRewardService _registrationRewardService;

    public SeguirNegocioService(
        INegocioRepository negocioRepository,
        INegocioUsuarioVinculacionRepository vinculacionRepository,
        INegocioUsuarioVinculacionService vinculacionService,
        IRegistrationRewardService registrationRewardService)
    {
        _negocioRepository = negocioRepository;
        _vinculacionRepository = vinculacionRepository;
        _vinculacionService = vinculacionService;
        _registrationRewardService = registrationRewardService;
    }

    public async Task<ServiceResult<SeguirNegocioResponse>> SeguirAsync(
        Guid negocioId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (negocioId == Guid.Empty || userId == Guid.Empty)
        {
            return ServiceResult<SeguirNegocioResponse>.Failure(
                "validation_error",
                "Negocio y usuario son obligatorios.");
        }

        Negocio? negocio = await _negocioRepository.GetByIdAsync(negocioId, cancellationToken);
        if (negocio is null || !negocio.Activo || !negocio.PublicadoPortal)
        {
            return ServiceResult<SeguirNegocioResponse>.Failure("not_found", "Negocio no encontrado.");
        }

        NegocioUsuarioVinculacion? existing =
            await _vinculacionRepository.GetByNegocioAndUserAsync(negocioId, userId, cancellationToken);

        if (IsActive(existing))
        {
            return ServiceResult<SeguirNegocioResponse>.Success(new SeguirNegocioResponse
            {
                NegocioId = negocioId,
                VinculacionId = existing!.Id,
                YaEstabaVinculado = true,
                VinculadoAhora = false,
                BonoBienvenidaRecibido = false
            });
        }

        bool isFirstLink = existing is null;
        Dynamic.Negocios.Application.Common.ServiceResult<NegocioUsuarioVinculacionResponse> linkResult =
            await _vinculacionService.LinkUserAsync(
                negocioId,
                userId,
                new VincularUsuarioNegocioRequest
                {
                    TipoVinculacion = TipoVinculacionNegocioUsuario.Cliente,
                    TituloRelacion = "Cliente",
                    EsPrincipal = false,
                    PuedeAccederBackoffice = false,
                    PuedeGestionarNegocio = false,
                    PuedeGestionarClientes = false,
                    PuedeGestionarCampanas = false,
                    PuedeGestionarPuntos = false,
                    PuedeValidarTickets = false,
                    PuedeVerReportes = false,
                    OrigenVinculacion = "business_follow"
                },
                linkedByUserId: userId,
                cancellationToken: cancellationToken);

        if (!linkResult.Succeeded || linkResult.Data is null)
        {
            return ServiceResult<SeguirNegocioResponse>.Failure(
                linkResult.ErrorCode ?? "validation_error",
                linkResult.ErrorMessage ?? "No se ha podido seguir el negocio.");
        }

        bool welcomeTicketAssigned = isFirstLink &&
            await _registrationRewardService.AssignBusinessWelcomeTicketAsync(negocioId, userId, cancellationToken);

        return ServiceResult<SeguirNegocioResponse>.Success(new SeguirNegocioResponse
        {
            NegocioId = negocioId,
            VinculacionId = linkResult.Data.VinculacionId,
            YaEstabaVinculado = false,
            VinculadoAhora = true,
            BonoBienvenidaRecibido = welcomeTicketAssigned
        });
    }

    public async Task<ServiceResult> DejarDeSeguirAsync(
        Guid negocioId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (negocioId == Guid.Empty || userId == Guid.Empty)
        {
            return ServiceResult.Failure("validation_error", "Negocio y usuario son obligatorios.");
        }

        NegocioUsuarioVinculacion? existing =
            await _vinculacionRepository.GetByNegocioAndUserAsync(negocioId, userId, cancellationToken);

        if (existing is null || !existing.Activa)
        {
            return ServiceResult.Success();
        }

        if (existing.TipoVinculacion != TipoVinculacionNegocioUsuario.Cliente)
        {
            return ServiceResult.Failure(
                "forbidden",
                "No puedes eliminar una vinculacion laboral o de gestion mediante la accion de dejar de seguir.");
        }

        Dynamic.Negocios.Application.Common.ServiceResult unlinkResult =
            await _vinculacionService.UnlinkUserAsync(
                negocioId,
                userId,
                unlinkedByUserId: userId,
                cancellationToken: cancellationToken);

        return unlinkResult.Succeeded
            ? ServiceResult.Success()
            : ServiceResult.Failure(
                unlinkResult.ErrorCode ?? "validation_error",
                unlinkResult.ErrorMessage ?? "No se ha podido dejar de seguir el negocio.");
    }

    private static bool IsActive(NegocioUsuarioVinculacion? vinculacion)
    {
        if (vinculacion is null || !vinculacion.Activa || vinculacion.RevokedAtUtc.HasValue)
        {
            return false;
        }

        DateTime now = DateTime.UtcNow;
        return (!vinculacion.FechaInicioUtc.HasValue || vinculacion.FechaInicioUtc.Value <= now) &&
               (!vinculacion.FechaFinUtc.HasValue || vinculacion.FechaFinUtc.Value >= now);
    }
}
