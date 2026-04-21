using Dynamic.Fidelity.Application.Contracts.Repositories;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dynamic.Fidelity.Infrastructure.Repositories;

public class QrCampaignRepository : IQrCampaignRepository
{
    private readonly DynamicFidelityDbContext _dbContext;

    public QrCampaignRepository(DynamicFidelityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<QrCampaign?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
        => _dbContext.QrCampaigns.FirstOrDefaultAsync(qrCampaign => qrCampaign.Token == token, cancellationToken);

    public Task AddAsync(QrCampaign qrCampaign, CancellationToken cancellationToken = default)
        => _dbContext.QrCampaigns.AddAsync(qrCampaign, cancellationToken).AsTask();
}
