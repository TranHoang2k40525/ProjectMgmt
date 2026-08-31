using ProjectMgmt.Modules.DeliveryIntelligence.AiCore.Domain.Entities;

namespace ProjectMgmt.Modules.DeliveryIntelligence.AiCore.Domain.IRepositories;

public interface IAiModelRepository
{
    Task<AiModel?> GetActiveAsync(string taskType, CancellationToken cancellationToken = default);
    Task<AiPromptTemplate?> GetActivePromptAsync(string taskType, CancellationToken cancellationToken = default);
}
