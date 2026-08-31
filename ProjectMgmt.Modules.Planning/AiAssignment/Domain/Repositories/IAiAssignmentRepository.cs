using ProjectMgmt.Modules.Planning.AiAssignment.Domain.Entities;

namespace ProjectMgmt.Modules.Planning.AiAssignment.Domain.Repositories;

public interface IAiAssignmentRepository
{
    Task<AiAssignmentRun?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiAssignmentFeedbackRecord>> GetFeedbackAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    Task AddRunAsync(AiAssignmentRun run, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record AiAssignmentFeedbackRecord(AiAssignmentDecision Decision, string? FeatureSnapshot);
