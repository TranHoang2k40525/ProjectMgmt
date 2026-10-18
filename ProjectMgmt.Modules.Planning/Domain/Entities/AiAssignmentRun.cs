namespace Planning.Domain.Entities;

public class AiAssignmentRun
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? SprintId { get; set; }
    public Guid RequestedBy { get; set; }
    public string TriggerSource { get; set; } = string.Empty;
    public Guid? SourceGenerationLogId { get; set; }
    public string Strategy { get; set; } = string.Empty;
    public Guid? ModelId { get; set; }
    public string? Weights { get; set; }
    public string? CandidateUserIds { get; set; }
    public int IssueCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public int? LatencyMs { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
