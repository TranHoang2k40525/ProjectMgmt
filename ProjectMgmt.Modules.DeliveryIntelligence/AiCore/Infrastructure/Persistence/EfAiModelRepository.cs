using Microsoft.EntityFrameworkCore;
using ProjectMgmt.Modules.DeliveryIntelligence.AiCore.Domain.Entities;
using ProjectMgmt.Modules.DeliveryIntelligence.AiCore.Domain.Repositories;
using ProjectMgmt.Modules.DeliveryIntelligence.Infrastructure.Persistence;

namespace ProjectMgmt.Modules.DeliveryIntelligence.AiCore.Infrastructure.Persistence;

internal sealed class EfAiModelRepository(DeliveryIntelligenceDbContext dbContext) : IAiModelRepository
{
    public Task<AiModel?> GetActiveAsync(string taskType, CancellationToken cancellationToken = default) =>
        dbContext.AiModels.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TaskType == taskType && x.IsActive, cancellationToken);

    public Task<AiPromptTemplate?> GetActivePromptAsync(
        string taskType,
        CancellationToken cancellationToken = default) =>
        dbContext.AiPromptTemplates.AsNoTracking()
            .Where(x => x.TaskType == taskType && x.IsActive)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);
}
