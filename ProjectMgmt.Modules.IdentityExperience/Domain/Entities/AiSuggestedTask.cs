namespace IdentityExperience.Domain.Entities;

public class AiSuggestedTask
{
    public Guid Id { get; set; }
    public Guid AiGenerationLogId { get; set; }
    public int OrderIndex { get; set; }
    public string OriginalSummary { get; set; } = string.Empty;
    public string? OriginalDescription { get; set; }
    public string? OriginalAcceptanceCriteria { get; set; }
    public decimal? OriginalEstimatePoints { get; set; }
    public string? OriginalSuggestedSkills { get; set; }
    public string? FinalSummary { get; set; }
    public string? FinalDescription { get; set; }
    public string? FinalAcceptanceCriteria { get; set; }
    public decimal? FinalEstimatePoints { get; set; }
    public string UserAction { get; set; } = string.Empty;
    public decimal? EditDistanceRatio { get; set; }
    public string? RejectReason { get; set; }
    public Guid? CreatedIssueId { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
