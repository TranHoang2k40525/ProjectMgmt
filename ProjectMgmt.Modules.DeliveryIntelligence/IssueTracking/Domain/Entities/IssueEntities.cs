namespace ProjectMgmt.Modules.DeliveryIntelligence.IssueTracking.Domain.Entities;

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
}

public class IssueLink
{
    public Guid Id { get; set; }
    public Guid SourceIssueId { get; set; }
    public Guid TargetIssueId { get; set; }
    public string LinkType { get; set; } = string.Empty;
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class IssueWatcher
{
    public Guid IssueId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Comment
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public Guid UserId { get; set; }
    public Guid? ParentCommentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? MentionedUserIds { get; set; }
    public bool IsEdited { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class Attachment
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public Guid UploadedBy { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoredPath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ActivityLog
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? FieldName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Detail { get; set; }
    public string Source { get; set; } = "User";
    public DateTime CreatedAt { get; set; }
}

public class IssueStatusHistory
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public Guid? FromStatusId { get; set; }
    public Guid ToStatusId { get; set; }
    public string? FromCategory { get; set; }
    public string ToCategory { get; set; } = string.Empty;
    public Guid? ChangedBy { get; set; }
    public long? DurationSeconds { get; set; }
    public DateTime ChangedAt { get; set; }
}

public class IssueAssignmentHistory
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public Guid? FromAssigneeId { get; set; }
    public Guid? ToAssigneeId { get; set; }
    public Guid? AssignedBy { get; set; }
    public string AssignmentSource { get; set; } = "Manual";
    public Guid? AiCandidateId { get; set; }
    public decimal? StoryPointsAtTime { get; set; }
    public string? Reason { get; set; }
    public DateTime AssignedAt { get; set; }
}

public class Label
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#DFE1E6";
}

public class IssueLabel
{
    public Guid IssueId { get; set; }
    public Guid LabelId { get; set; }
}

public class IssueComponentLink
{
    public Guid IssueId { get; set; }
    public Guid ComponentId { get; set; }
}

public class IssueVersionLink
{
    public Guid IssueId { get; set; }
    public Guid VersionId { get; set; }
    public string LinkType { get; set; } = "FixVersion";
}

public class IssueRequiredSkill
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public Guid SkillId { get; set; }
    public int MinLevel { get; set; } = 1;
    public decimal Weight { get; set; } = 1m;
    public string Source { get; set; } = "Manual";
}

public class AcceptanceCriteria
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsMet { get; set; }
    public Guid? MetBy { get; set; }
    public DateTime? MetAt { get; set; }
    public int OrderIndex { get; set; }
    public string Source { get; set; } = "Manual";
    public Guid? AiGenerationLogId { get; set; }
    public bool WasEditedAfterAi { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
