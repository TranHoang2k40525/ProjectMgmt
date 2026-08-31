using Microsoft.EntityFrameworkCore;
using ProjectMgmt.Modules.DeliveryIntelligence.AiDataOps.Domain.Entities;
using ProjectMgmt.Modules.DeliveryIntelligence.AiDataOps.Domain.IRepositories;
using ProjectMgmt.Modules.DeliveryIntelligence.Infrastructure.Persistence;

namespace ProjectMgmt.Modules.DeliveryIntelligence.AiDataOps.Infrastructure.Repositories;

internal class AiDatasetRepository : IAiDatasetRepository
{
    private readonly DeliveryIntelligenceAppDbContext _dbContext;

    public AiDatasetRepository(DeliveryIntelligenceAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    public Task<AiDatasetVersion?> GetLatestFrozenVersionAsync(CancellationToken cancellationToken = default) =>
        _dbContext.AiDatasetVersions.AsNoTracking()
            .Where(x => x.IsFrozen && x.FrozenAt != null && x.Checksum != null)
            .OrderByDescending(x => x.FrozenAt)
            .FirstOrDefaultAsync(cancellationToken);
}
