using Dynamic.Fidelity.Application.Common;
using Dynamic.Fidelity.Application.DTOs.Responses;
using Dynamic.Negocios.Domain.Entities;

namespace Dynamic.Fidelity.Application.Contracts.Services;

public interface INegocioAudienciaService
{
    Task<ServiceResult<FormarParteNegocioResponse>> FormarParteAsync(
        Guid negocioId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> DejarDeFormarParteAsync(
        Guid negocioId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AudienceFavoriteResponse>> SetFavoritoAsync(
        Guid negocioId,
        Guid userId,
        bool esFavorito,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyCollection<UserPortalBusinessResponse>>> GetMyBusinessesAsync(
        Guid userId,
        bool soloFavoritos,
        IReadOnlyCollection<string>? tags,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyCollection<string>>> GetMyBusinessTagsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<NegocioAudiencia>> EnsureAudienceAsync(
        Guid negocioId,
        Guid userId,
        string origin,
        CancellationToken cancellationToken = default);

    Task TouchAudienceActivityAsync(
        Guid negocioId,
        Guid userId,
        string origin,
        CancellationToken cancellationToken = default);
}
