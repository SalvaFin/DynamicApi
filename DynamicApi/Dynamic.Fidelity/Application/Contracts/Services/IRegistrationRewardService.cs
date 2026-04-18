namespace Dynamic.Fidelity.Application.Contracts.Services;

public interface IRegistrationRewardService
{
    Task<bool> ValidateQrTokenAsync(string qrToken, CancellationToken cancellationToken = default);
    Task PreparePendingAssignmentAsync(Guid userId, string qrToken, CancellationToken cancellationToken = default);
    Task FinalizePendingAssignmentsAsync(Guid userId, CancellationToken cancellationToken = default);
}
