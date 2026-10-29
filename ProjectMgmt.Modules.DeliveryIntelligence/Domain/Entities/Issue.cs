namespace DeliveryIntelligence.Domain.Entities;

public class Issue
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public int IssueNumber { get; set; }
    public Guid? SprintId { get; set; }
    public Guid? ParentId { get; set; }
    public Guid? EpicId { get; set; }
    public Guid? AssigneeId { get; set; }
    public Guid ReporterId { get; set; }
    public Guid StatusId { get; set; }
    public Guid IssueTypeId { get; set; }
    public Guid? PriorityId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? StoryPoints { get; set; }
    public int? OriginalEstimateMinutes { get; set; }
    public int TimeSpentMinutes { get; set; }
    public decimal RankOrder { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public bool IsAiGenerated { get; set; }
    public Guid? AiGenerationLogId { get; set; }
    public bool IsAiAssigned { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Daily Scrum Blocker Flag & Subtask Hours
    public bool IsBlocked { get; set; }
    public string? BlockReason { get; set; }
    public DateTime? BlockedAt { get; set; }
    public decimal? EstimatedHours { get; set; }
    public decimal? RemainingHours { get; set; }
}
