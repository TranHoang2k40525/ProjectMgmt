namespace ProjectMgmt.Solution.Dtos.Ai;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Kế hoạch xác nhận gỡ khỏi Sprint hoặc xóa công việc do Trợ Lý AI chuẩn bị trước khi thực thi.
/// </summary>
public sealed class AiTaskRemovalPlanDto
{
    public string PlanId { get; set; } = Guid.NewGuid().ToString("N");
    public string RequestedMode { get; set; } = "detach_to_backlog"; // "detach_to_backlog" | "soft_delete"
    public bool CanDetachToBacklog { get; set; }
    public bool CanDelete { get; set; }
    public bool HasAppliedFromSessionMatch { get; set; }
    public string TargetScopeDescription { get; set; } = string.Empty;
    public string Status { get; set; } = "pending"; // "pending" | "executed" | "cancelled"
    public string? ExecutedMode { get; set; }
    public string? CompletedMessage { get; set; }
    public List<AiRemovalCandidateIssueDto> Candidates { get; set; } = [];
}

/// <summary>
/// Thông tin một công việc thực tế được đề xuất gỡ khỏi Sprint hoặc xóa (kèm số việc con bị ảnh hưởng).
/// </summary>
public sealed class AiRemovalCandidateIssueDto
{
    public Guid IssueId { get; set; }
    public string IssueKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string IssueType { get; set; } = "Story";
    public Guid? SprintId { get; set; }
    public string? SprintName { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public string Priority { get; set; } = "Medium";
    public decimal StoryPoints { get; set; }
    public string? AssigneeName { get; set; }
    public int SubtaskCount { get; set; }
    public List<string> ChildIssueKeys { get; set; } = [];
    public bool WasMovedFromBacklog { get; set; }
    public bool IsAiGenerated { get; set; }
    public bool SelectedByDefault { get; set; } = true;
}

/// <summary>
/// Yêu cầu xác nhận thực thi gỡ hoặc xóa công việc sau khi người dùng đã kiểm tra bảng xác nhận.
/// </summary>
public sealed class AiConfirmTaskRemovalRequest
{
    [Required]
    public string SessionId { get; set; } = string.Empty;

    public Guid? ProjectId { get; set; }

    public string? PlanId { get; set; }

    /// <summary>
    /// Chế độ xác nhận: "detach_to_backlog" | "soft_delete" | "cancel"
    /// </summary>
    [Required]
    public string Mode { get; set; } = "detach_to_backlog";

    public List<Guid> IssueIds { get; set; } = [];
}

/// <summary>
/// Kết quả sau khi người dùng xác nhận gỡ khỏi Sprint hoặc xóa mềm công việc.
/// </summary>
public sealed class AiConfirmTaskRemovalResponse
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public int AffectedParentCount { get; set; }
    public int AffectedSubtaskCount { get; set; }
    public int TotalAffectedCount { get; set; }
    public List<string> AffectedIssueKeys { get; set; } = [];
}
