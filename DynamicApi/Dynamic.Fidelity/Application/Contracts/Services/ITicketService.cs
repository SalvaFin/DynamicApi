using Dynamic.Fidelity.Application.Common;
using Dynamic.Fidelity.Application.DTOs.Requests;
using Dynamic.Fidelity.Application.DTOs.Responses;

namespace Dynamic.Fidelity.Application.Contracts.Services;

public interface ITicketService
{
    Task<ServiceResult<IReadOnlyCollection<TicketResponse>>> GetPublicGeneralTicketsAsync(
        Guid negocioId,
        bool includeWelcomeTicket = false,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyCollection<TicketResponse>>> GetPublicGeneralTicketsBySlugAsync(
        string slugPortal,
        bool includeWelcomeTicket = false,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyCollection<TicketResponse>>> GetAllAsync(
        Guid negocioId,
        Guid requesterUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<TicketResponse>> GetByIdAsync(
        Guid negocioId,
        Guid ticketId,
        Guid requesterUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<TicketResponse>> CreateAsync(
        Guid negocioId,
        Guid requesterUserId,
        bool isAdmin,
        CreateTicketRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<TicketResponse>> CreateWelcomeTicketAsync(
        Guid negocioId,
        Guid requesterUserId,
        bool isAdmin,
        CreateTicketRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<TicketResponse>> CreateReferralTicketAsync(
        Guid negocioId,
        Guid requesterUserId,
        bool isAdmin,
        CreateTicketRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<TicketResponse>> UpdateAsync(
        Guid negocioId,
        Guid ticketId,
        Guid requesterUserId,
        bool isAdmin,
        UpdateTicketRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<TicketResponse>> UnlockAsync(
        Guid negocioId,
        Guid ticketId,
        Guid requesterUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> DeleteAsync(
        Guid negocioId,
        Guid ticketId,
        Guid requesterUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default);
}
