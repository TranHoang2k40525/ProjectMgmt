using ProjectMgmt.Modules.Planning.AiAssignment.Domain.Entities;

namespace ProjectMgmt.Modules.Planning.AiAssignment.Domain.IRepositories;

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

public class AiAssignmentFeedbackRecord
{
    public AiAssignmentFeedbackRecord(AiAssignmentDecision decision, string? featureSnapshot)
    {
        Decision = decision;
        FeatureSnapshot = featureSnapshot;
    }

    public AiAssignmentDecision Decision { get; }
    public string? FeatureSnapshot { get; }
}
