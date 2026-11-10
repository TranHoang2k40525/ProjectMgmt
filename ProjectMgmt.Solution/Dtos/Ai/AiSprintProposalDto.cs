namespace ProjectMgmt.Solution.Dtos.Ai;

/// <summary>
/// Full structured sprint proposal staged in IStateStagingService.
/// </summary>
public sealed class AiSprintProposalDto
{
    public string SessionId { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public Guid? TargetSprintId { get; set; }
    public string SprintGoal { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public decimal TotalEstimatedHours { get; set; }
    public decimal TotalStoryPoints { get; set; }
    public List<ProposedTaskDto> Tasks { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ProposedTaskDto
{
    public string Id { get; set; } = string.Empty; // e.g. "task_01" or GUID string
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
    public Guid? AssigneeUserId { get; set; }
    public string? AssigneeName { get; set; }
}
