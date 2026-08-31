namespace ProjectMgmt.Modules.IdentityExperience.AiAssist.Domain.Entities;

public sealed class AiGenerationLog
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
    public Guid? ModelId { get; set; }
    public Guid? PromptTemplateId { get; set; }
    public string? InputText { get; set; }
    public string? RenderedPrompt { get; set; }
    public string? RawResponseJson { get; set; }
    public string? ParsedJson { get; set; }
    public string Status { get; set; } = "Pending";
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int? LatencyMs { get; set; }
    public int RetryCount { get; set; }
    public string? HangfireJobId { get; set; }
    public DateTime? AppliedAt { get; set; }
    public int AppliedCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public sealed class AiSuggestedTask
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
    public string UserAction { get; set; } = "Pending";
    public decimal? EditDistanceRatio { get; set; }
    public string? RejectReason { get; set; }
    public Guid? CreatedIssueId { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
