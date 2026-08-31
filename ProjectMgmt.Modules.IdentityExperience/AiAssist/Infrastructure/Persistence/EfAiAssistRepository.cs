using Microsoft.EntityFrameworkCore;
using ProjectMgmt.Modules.IdentityExperience.AiAssist.Domain.Entities;
using ProjectMgmt.Modules.IdentityExperience.AiAssist.Domain.Repositories;
using ProjectMgmt.Modules.IdentityExperience.Infrastructure.Persistence;

namespace ProjectMgmt.Modules.IdentityExperience.AiAssist.Infrastructure.Persistence;

internal sealed class EfAiAssistRepository(IdentityExperienceDbContext dbContext) : IAiAssistRepository
{
    public Task<AiGenerationLog?> GetGenerationLogAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.AiGenerationLogs.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AiSuggestedTask>> GetReviewedTasksAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default) =>
        await dbContext.AiSuggestedTasks.AsNoTracking()
            .Where(x => x.ReviewedAt != null && x.ReviewedAt >= fromUtc && x.ReviewedAt < toUtc)
            .OrderBy(x => x.ReviewedAt)
            .ToListAsync(cancellationToken);

    public async Task AddGenerationLogAsync(AiGenerationLog generationLog, CancellationToken cancellationToken = default) =>
        await dbContext.AiGenerationLogs.AddAsync(generationLog, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.SaveChangesAsync(cancellationToken);
}
