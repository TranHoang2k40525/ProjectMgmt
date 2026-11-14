namespace ProjectMgmt.Solution.Dtos.Ai;

using System.ComponentModel.DataAnnotations;

public sealed class AiApplySprintProposalRequest
{
    [Required]
    public string SessionId { get; set; } = string.Empty;

    public Guid? ProjectId { get; set; }

    public Guid? TargetSprintId { get; set; }

    /// <summary>
    /// When true, pushes the proposed work items and subtasks directly into the Product Backlog (SprintId = null)
    /// instead of assigning them to a Sprint.
    /// </summary>
    public bool PushToBacklog { get; set; }

    public string SprintGoal { get; set; } = string.Empty;

    public List<ProposedTaskDto> Tasks { get; set; } = [];
}

public sealed class AiUpdateProposalRequest
{
    [Required]
    public string SprintGoal { get; set; } = string.Empty;

    public string? Rationale { get; set; }

    public Guid? TargetSprintId { get; set; }

    public List<ProposedTaskDto> Tasks { get; set; } = [];
}

public sealed class AiRegenerateAllRequest
{
    [Required]
    public string SessionId { get; set; } = string.Empty;

    [Required]
    public string Feedback { get; set; } = string.Empty;
}

public sealed class AiApplySprintProposalResponse
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid SprintId { get; set; }
    public string SprintName { get; set; } = string.Empty;
    public string SprintGoal { get; set; } = string.Empty;
    public int CreatedIssueCount { get; set; }
    public int CreatedTaskCount { get; set; }
    public int CreatedSubtaskCount { get; set; }
    public int MovedFromBacklogCount { get; set; }
    public int RolloverTaskCount { get; set; }
    public int BacklogReturnedCount { get; set; }
    public decimal TotalStoryPoints { get; set; }
    public List<string> Warnings { get; set; } = [];
    public List<AiCreatedIssueItemDto> CreatedIssues { get; set; } = [];
}

public sealed class AiCreatedIssueItemDto
{
    public Guid IssueId { get; set; }
    public string IssueKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Guid? ParentIssueId { get; set; }
    public decimal? StoryPoints { get; set; }
}

