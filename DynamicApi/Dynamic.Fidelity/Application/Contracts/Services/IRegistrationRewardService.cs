using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Application.Models;

namespace Dynamic.Fidelity.Application.Contracts.Services;

public interface IRegistrationRewardService
{
    Task<bool> ValidateQrTokenAsync(string qrToken, CancellationToken cancellationToken = default);
    Task PreparePendingAssignmentAsync(Guid userId, string qrToken, CancellationToken cancellationToken = default);
    Task FinalizePendingAssignmentsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<WelcomeTicketClaimResult?> ClaimTicketFromQrAsync(Guid userId, string qrToken, CancellationToken cancellationToken = default);
    Task<bool> AssignBusinessWelcomeTicketAsync(Guid negocioId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> AssignBusinessReferralTicketAsync(Guid negocioId, Guid userId, CancellationToken cancellationToken = default);
}
