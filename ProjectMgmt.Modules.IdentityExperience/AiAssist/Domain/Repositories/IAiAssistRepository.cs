using ProjectMgmt.Modules.IdentityExperience.AiAssist.Domain.Entities;

namespace ProjectMgmt.Modules.IdentityExperience.AiAssist.Domain.Repositories;

public interface IAiAssistRepository
{
    Task<AiGenerationLog?> GetGenerationLogAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiSuggestedTask>> GetReviewedTasksAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    Task AddGenerationLogAsync(AiGenerationLog generationLog, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
