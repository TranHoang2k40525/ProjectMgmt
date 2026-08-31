using Microsoft.EntityFrameworkCore;
using ProjectMgmt.Modules.DeliveryIntelligence.AiCore.Domain.Entities;
using ProjectMgmt.Modules.DeliveryIntelligence.AiCore.Domain.IRepositories;
using ProjectMgmt.Modules.DeliveryIntelligence.Infrastructure.Persistence;

namespace ProjectMgmt.Modules.DeliveryIntelligence.AiCore.Infrastructure.Repositories;

internal class AiModelRepository : IAiModelRepository
{
    private readonly DeliveryIntelligenceAppDbContext _dbContext;

    public AiModelRepository(DeliveryIntelligenceAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    public Task<AiModel?> GetActiveAsync(string taskType, CancellationToken cancellationToken = default) =>
        _dbContext.AiModels.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TaskType == taskType && x.IsActive, cancellationToken);

    public Task<AiPromptTemplate?> GetActivePromptAsync(
        string taskType,
        CancellationToken cancellationToken = default) =>
        _dbContext.AiPromptTemplates.AsNoTracking()
            .Where(x => x.TaskType == taskType && x.IsActive)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);
}
