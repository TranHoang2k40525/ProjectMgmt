namespace ProjectMgmt.AiAssignment.Contracts;

public class AiAssignmentFeedbackSampleDto
{
    public AiAssignmentFeedbackSampleDto(
        Guid decisionId,
        Guid issueId,
        Guid? suggestedUserId,
        Guid? finalUserId,
        string outcome,
        string? overrideReason,
        IReadOnlyDictionary<string, decimal> featureSnapshot,
        DateTimeOffset decidedAtUtc)
    {
        DecisionId = decisionId;
        IssueId = issueId;
        SuggestedUserId = suggestedUserId;
        FinalUserId = finalUserId;
        Outcome = outcome;
        OverrideReason = overrideReason;
        FeatureSnapshot = featureSnapshot;
        DecidedAtUtc = decidedAtUtc;
    }

    public Guid DecisionId { get; set; }
    public Guid IssueId { get; set; }
    public Guid? SuggestedUserId { get; set; }
    public Guid? FinalUserId { get; set; }
    public string Outcome { get; set; }
    public string? OverrideReason { get; set; }
    public IReadOnlyDictionary<string, decimal> FeatureSnapshot { get; set; }
    public DateTimeOffset DecidedAtUtc { get; set; }
}

public interface IAiAssignmentFeedbackExportService
{
    Task<IReadOnlyList<AiAssignmentFeedbackSampleDto>> ExportAssignmentSamplesAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default);
}
