namespace IdentityExperience.Application.Dto;

public class AiBreakdownDto : Result
{
    public Guid? GenerationId { get; set; }
    public Guid? IssueId { get; set; }
    public Guid? ParentIssueId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? SuggestedTaskId { get; set; }
    public Guid? CreatedIssueId { get; set; }
    public string? IssueKey { get; set; }
    public string? Status { get; set; }
    public string? ModelVersion { get; set; }
    public string? PromptVersion { get; set; }
    public string? StoryTitle { get; set; }
    public string? StoryDescription { get; set; }
    public string? ProjectContext { get; set; }
    public string? TempId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public decimal? EstimatedHours { get; set; }
    public decimal? EstimatePoints { get; set; }
    public List<string>? AcceptanceCriteria { get; set; }
    public List<string>? SuggestedSkills { get; set; }
    public string? UserAction { get; set; }
    public string? FinalSummary { get; set; }
    public string? FinalDescription { get; set; }
    public List<string>? FinalAcceptanceCriteria { get; set; }
    public decimal? FinalEstimatePoints { get; set; }
    public decimal? EditDistanceRatio { get; set; }
    public string? RejectReason { get; set; }
    public bool? FeedbackLogged { get; set; }
    public bool? Applied { get; set; }
    public int? AppliedCount { get; set; }
    public List<AiBreakdownDto>? SuggestedSubTasks { get; set; }
    public List<AiBreakdownDto>? SelectedSubTasks { get; set; }
    public List<AiBreakdownDto>? CreatedSubTasks { get; set; }
}

public class AiPromptTemplateDto : Result
{
    public Guid? PromptId { get; set; }
    public string? Code { get; set; }
    public int? Version { get; set; }
    public string? TaskType { get; set; }
    public string? Language { get; set; }
    public string? SystemPrompt { get; set; }
    public string? UserTemplate { get; set; }
    public string? JsonSchema { get; set; }
    public bool? IsActive { get; set; }
    public DateTime? CreatedAt { get; set; }
    public List<AiPromptTemplateDto>? Items { get; set; }
}
