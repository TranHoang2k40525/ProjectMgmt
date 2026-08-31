using Microsoft.EntityFrameworkCore;
using ProjectMgmt.Modules.DeliveryIntelligence.AiDataOps.Domain.Entities;
using ProjectMgmt.Modules.DeliveryIntelligence.AiDataOps.Domain.Repositories;
using ProjectMgmt.Modules.DeliveryIntelligence.Infrastructure.Persistence;

namespace ProjectMgmt.Modules.DeliveryIntelligence.AiDataOps.Infrastructure.Persistence;

internal sealed class EfAiDatasetRepository(DeliveryIntelligenceDbContext dbContext) : IAiDatasetRepository
{
    public Task<AiDatasetVersion?> GetLatestFrozenVersionAsync(CancellationToken cancellationToken = default) =>
        dbContext.AiDatasetVersions.AsNoTracking()
            .Where(x => x.IsFrozen && x.FrozenAt != null && x.Checksum != null)
            .OrderByDescending(x => x.FrozenAt)
            .FirstOrDefaultAsync(cancellationToken);
}
