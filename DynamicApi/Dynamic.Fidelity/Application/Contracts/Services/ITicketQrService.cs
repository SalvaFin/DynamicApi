using Dynamic.Fidelity.Application.Common;
using Dynamic.Fidelity.Application.DTOs.Responses;

namespace Dynamic.Fidelity.Application.Contracts.Services;

public interface ITicketQrService
{
    Task<ServiceResult<TicketQrResponse>> GenerateTicketQrAsync(
        Guid negocioId,
        Guid ticketId,
        Guid requesterUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<TicketQrLookupResponse>> GetTicketByQrAsync(
        string slugPortal,
        string qrToken,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<TicketQrScanResponse>> ScanTicketQrAsync(
        Guid userId,
        string qrToken,
        CancellationToken cancellationToken = default);
}
