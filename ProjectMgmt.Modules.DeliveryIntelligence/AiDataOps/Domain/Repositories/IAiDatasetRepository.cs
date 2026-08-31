using ProjectMgmt.Modules.DeliveryIntelligence.AiDataOps.Domain.Entities;

namespace ProjectMgmt.Modules.DeliveryIntelligence.AiDataOps.Domain.Repositories;

public interface IAiDatasetRepository
{
    Task<AiDatasetVersion?> GetLatestFrozenVersionAsync(CancellationToken cancellationToken = default);
}
