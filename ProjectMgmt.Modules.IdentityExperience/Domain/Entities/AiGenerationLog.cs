namespace IdentityExperience.Domain.Entities;

public class AiGenerationLog
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
    public string Status { get; set; } = string.Empty;
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
