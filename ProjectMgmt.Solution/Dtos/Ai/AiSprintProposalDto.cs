namespace ProjectMgmt.Solution.Dtos.Ai;

/// <summary>
/// Represents a single turn in the stateful multi-turn AI Assistant conversation stored in Redis.
/// </summary>
public sealed class AiConversationTurnDto
{
    public string Id { get; set; } = string.Empty;
    public string Role { get; set; } = "user"; // "user" | "assistant"
    public string Content { get; set; } = string.Empty;
    public string? FullContent { get; set; }
    public string? FileName { get; set; }
    public string Intent { get; set; } = "sprint_plan"; // "sprint_plan" | "backlog_plan" | "refine_proposal" | "export_excel" | "push_backlog" | "apply_sprint" | "project_qa" | "out_of_scope_refusal"
    public string? ActionType { get; set; } // "none" | "export_excel" | "push_backlog" | "apply_sprint"
    public int? TaskCount { get; set; }
    public decimal? TotalStoryPoints { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Full structured sprint/backlog proposal & stateful conversation session staged in IStateStagingService (Redis).
/// </summary>
public sealed class AiSprintProposalDto
{
    public string SessionId { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public Guid? TargetSprintId { get; set; }
    public string SprintGoal { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public string Intent { get; set; } = "sprint_plan";
    public string? AssistantReply { get; set; }
    public string RecommendedTarget { get; set; } = "sprint"; // "sprint" | "backlog"
    public string? TriggerAction { get; set; } // "none" | "export_excel" | "push_backlog" | "apply_sprint"
    public List<AiConversationTurnDto> ConversationHistory { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public decimal TotalEstimatedHours { get; set; }
    public decimal TotalStoryPoints { get; set; }
    public string? WorkflowName { get; set; }
    public string? InitialStatus { get; set; }
    public List<string> AppliedWorkflowStatuses { get; set; } = [];
    public List<string> AppliedPriorities { get; set; } = [];
    public List<string> AppliedIssueTypes { get; set; } = [];
    public List<string> AppliedLabels { get; set; } = [];
    public List<ProposedTaskDto> Tasks { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ProposedTaskDto
{
    public string Id { get; set; } = string.Empty; // e.g. "task_01" or GUID string
    public Guid? SourceBacklogIssueId { get; set; }
    public string? SourceBacklogIssueKey { get; set; }
    public Guid? EpicId { get; set; }
    public string? EpicTitle { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = "Story"; // Story, Task, Bug, TechnicalDebt
    public string Priority { get; set; } = "Medium"; // Highest, High, Medium, Low
    public string Status { get; set; } = "To Do";
    public bool IsReady { get; set; } = true;
    public decimal StoryPoints { get; set; } = 3;
    public decimal EstimatedHours { get; set; } = 8;
    public decimal RemainingHours { get; set; } = 8;
    public Guid? AssigneeUserId { get; set; }
    public string? AssigneeName { get; set; }
    public List<string> Tags { get; set; } = [];
    public List<string> SuggestedSkills { get; set; } = [];
    public List<string> AcceptanceCriteria { get; set; } = [];
    public List<string> DocumentCitations { get; set; } = [];
    public Guid? DocumentId { get; set; }
    public string? AiRationale { get; set; }
    public List<ProposedSubTaskDto> Subtasks { get; set; } = [];
}

public sealed class ProposedSubTaskDto
{
    public string Id { get; set; } = string.Empty; // e.g. "subtask_01_1"
    public string Title { get; set; } = string.Empty;
    public string Role { get; set; } = "Backend"; // Backend, Frontend, QA, DevOps
    public decimal EstimatedHours { get; set; } = 4;
    public int StoryPoints { get; set; } = 1;
    public string Priority { get; set; } = "Medium";
    public string Status { get; set; } = "To Do";
    public string Description { get; set; } = string.Empty;
    public Guid? AssigneeUserId { get; set; }
    public string? AssigneeName { get; set; }
    public List<string> AcceptanceCriteria { get; set; } = [];
    public List<string> DocumentCitations { get; set; } = [];
    public Guid? DocumentId { get; set; }
}
