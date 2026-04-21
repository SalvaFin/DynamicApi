using Dynamic.Fidelity.Domain.Entities;

namespace Dynamic.Fidelity.Application.Contracts.Repositories;

public interface IQrCampaignRepository
{
    Task<QrCampaign?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task AddAsync(QrCampaign qrCampaign, CancellationToken cancellationToken = default);
}
