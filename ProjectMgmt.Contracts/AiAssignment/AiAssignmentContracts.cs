namespace ProjectMgmt.AiAssignment.Contracts;

public sealed record AiAssignmentFeedbackSampleDto(
    Guid DecisionId,
    Guid IssueId,
    Guid? SuggestedUserId,
    Guid? FinalUserId,
    string Outcome,
    string? OverrideReason,
    IReadOnlyDictionary<string, decimal> FeatureSnapshot,
    DateTimeOffset DecidedAtUtc);

public interface IAiAssignmentFeedbackExportService
{
    Task<IReadOnlyList<AiAssignmentFeedbackSampleDto>> ExportAssignmentSamplesAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default);
}
