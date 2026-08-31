using Microsoft.EntityFrameworkCore;
using ProjectMgmt.Modules.IdentityExperience.AiAssist.Domain.Entities;
using ProjectMgmt.Modules.IdentityExperience.AiAssist.Domain.IRepositories;
using ProjectMgmt.Modules.IdentityExperience.Infrastructure.Persistence;

namespace ProjectMgmt.Modules.IdentityExperience.AiAssist.Infrastructure.Repositories;

internal class AiAssistRepository : IAiAssistRepository
{
    private readonly IdentityExperienceAppDbContext _dbContext;

    public AiAssistRepository(IdentityExperienceAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    public Task<AiGenerationLog?> GetGenerationLogAsync(Guid id, CancellationToken cancellationToken = default) =>
        _dbContext.AiGenerationLogs.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AiSuggestedTask>> GetReviewedTasksAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default) =>
        await _dbContext.AiSuggestedTasks.AsNoTracking()
            .Where(x => x.ReviewedAt != null && x.ReviewedAt >= fromUtc && x.ReviewedAt < toUtc)
            .OrderBy(x => x.ReviewedAt)
            .ToListAsync(cancellationToken);

    public async Task AddGenerationLogAsync(AiGenerationLog generationLog, CancellationToken cancellationToken = default) =>
        await _dbContext.AiGenerationLogs.AddAsync(generationLog, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.SaveChangesAsync(cancellationToken);
}
