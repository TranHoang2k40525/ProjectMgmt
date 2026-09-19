namespace Planning.Domain.Entities;

public class AiAssignmentDecision
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public Guid IssueId { get; set; }
    public Guid? SuggestedUserId { get; set; }
    public Guid? SuggestedCandidateId { get; set; }
    public Guid? FinalUserId { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public string? OverrideReason { get; set; }
    public Guid? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    public decimal? ActualCycleTimeHours { get; set; }
    public bool? WasCompletedOnTime { get; set; }
    public bool WasReassignedLater { get; set; }
    public DateTime? OutcomeEvaluatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
