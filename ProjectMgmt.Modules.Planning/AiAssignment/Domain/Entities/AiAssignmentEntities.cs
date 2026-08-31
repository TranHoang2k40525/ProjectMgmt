namespace ProjectMgmt.Modules.Planning.AiAssignment.Domain.Entities;

public sealed class UserWorkloadSnapshot
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? SprintId { get; set; }
    public DateOnly SnapshotDate { get; set; }
    public int OpenIssueCount { get; set; }
    public int InProgressCount { get; set; }
    public decimal OpenPoints { get; set; }
    public decimal InProgressPoints { get; set; }
    public int OverdueCount { get; set; }
    public decimal? CapacityPoints { get; set; }
    public decimal? UtilizationRatio { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class UserPerformanceMetric
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? SprintId { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public int AssignedIssueCount { get; set; }
    public int CompletedIssueCount { get; set; }
    public decimal CommittedPoints { get; set; }
    public decimal CompletedPoints { get; set; }
    public decimal? AvgCycleTimeHours { get; set; }
    public decimal? MedianCycleTimeHours { get; set; }
    public decimal? OnTimeRatio { get; set; }
    public int ReopenedCount { get; set; }
    public decimal? EstimateAccuracyRatio { get; set; }
    public DateTime CalculatedAt { get; set; }
}

public sealed class AiAssignmentRun
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? SprintId { get; set; }
    public Guid RequestedBy { get; set; }
    public string TriggerSource { get; set; } = "Manual";
    public Guid? SourceGenerationLogId { get; set; }
    public string Strategy { get; set; } = "WeightedScore";
    public Guid? ModelId { get; set; }
    public string? Weights { get; set; }
    public string? CandidateUserIds { get; set; }
    public int IssueCount { get; set; }
    public string Status { get; set; } = "Pending";
    public string? ErrorMessage { get; set; }
    public int? LatencyMs { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public sealed class AiAssignmentCandidate
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public Guid IssueId { get; set; }
    public Guid CandidateUserId { get; set; }
    public int Rank { get; set; }
    public decimal TotalScore { get; set; }
    public decimal? LoadBalanceScore { get; set; }
    public decimal? SkillMatchScore { get; set; }
    public decimal? HistoryScore { get; set; }
    public decimal? CapacityScore { get; set; }
    public bool IsColdStart { get; set; }
    public string FeatureSnapshot { get; set; } = "{}";
    public string? Explanation { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class AiAssignmentDecision
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public Guid IssueId { get; set; }
    public Guid? SuggestedUserId { get; set; }
    public Guid? SuggestedCandidateId { get; set; }
    public Guid? FinalUserId { get; set; }
    public string Outcome { get; set; } = "Pending";
    public string? OverrideReason { get; set; }
    public Guid? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    public decimal? ActualCycleTimeHours { get; set; }
    public bool? WasCompletedOnTime { get; set; }
    public bool WasReassignedLater { get; set; }
    public DateTime? OutcomeEvaluatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
