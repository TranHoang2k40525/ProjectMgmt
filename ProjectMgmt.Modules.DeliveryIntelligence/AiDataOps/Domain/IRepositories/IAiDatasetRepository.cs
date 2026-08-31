using ProjectMgmt.Modules.DeliveryIntelligence.AiDataOps.Domain.Entities;

namespace ProjectMgmt.Modules.DeliveryIntelligence.AiDataOps.Domain.IRepositories;

public interface IAiDatasetRepository
{
    Task<AiDatasetVersion?> GetLatestFrozenVersionAsync(CancellationToken cancellationToken = default);
}
