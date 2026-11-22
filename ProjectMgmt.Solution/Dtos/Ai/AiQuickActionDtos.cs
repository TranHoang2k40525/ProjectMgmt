namespace ProjectMgmt.Solution.Dtos.Ai;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Yêu cầu chạy Trợ Lý AI không qua khung chat cho một công việc cụ thể (Phân rã Sub-task hoặc Đề xuất phân công).
/// Kết quả chỉ được lưu tạm trên Redis cho tới khi người dùng bấm Xác nhận áp dụng.
/// </summary>
public sealed class AiQuickActionRunRequest
{
    [Required]
    public Guid ProjectId { get; set; }

    [Required]
    public Guid IssueId { get; set; }

    /// <summary>
    /// Loại thao tác AI: "subtask_breakdown" | "assignee_suggestion"
    /// </summary>
    [Required]
    public string ActionType { get; set; } = "subtask_breakdown";

    public string? Model { get; set; }

    public string? CustomInstruction { get; set; }
}

/// <summary>
/// Kế hoạch thao tác AI nhanh cho một công việc, được lưu tạm trên Redis (TTL 2 giờ) chờ người dùng xác nhận áp dụng.
/// </summary>
public sealed class AiQuickActionPlanDto
{
    public string ActionId { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public Guid IssueId { get; set; }
    public string IssueKey { get; set; } = string.Empty;
    public string IssueTitle { get; set; } = string.Empty;
    public string IssueType { get; set; } = string.Empty;
    public Guid? SprintId { get; set; }
    public string SprintName { get; set; } = "Product Backlog";
    public string ActionType { get; set; } = "subtask_breakdown"; // "subtask_breakdown" | "assignee_suggestion"
    public string Status { get; set; } = "pending"; // "pending" | "executed" | "cancelled"
    public string? ExecutedMode { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public bool StagedInRedis { get; set; } = true;
    public bool UsedLlm { get; set; }
    public string? ModelUsed { get; set; }
    public List<string> Warnings { get; set; } = [];
    public List<string> DocumentCitations { get; set; } = [];
    public List<AiTurnTraceItemDto> TraceItems { get; set; } = [];
    public List<AiQuickSubtaskCandidateDto> ProposedSubtasks { get; set; } = [];
    public List<AiQuickAssigneeCandidateDto> AssigneeCandidates { get; set; } = [];
    public Guid? RecommendedUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExecutedAtUtc { get; set; }
    public string? CompletedMessage { get; set; }
}

/// <summary>
/// Ứng viên Sub-task do Trợ Lý AI đề xuất trên Redis chờ người dùng duyệt và xác nhận.
/// </summary>
public sealed class AiQuickSubtaskCandidateDto
{
    public string TempId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Role { get; set; } = "Backend";
    public decimal EstimatedHours { get; set; } = 4m;
    public int StoryPoints { get; set; } = 1;
    public string Priority { get; set; } = "Medium";
    public Guid? SuggestedAssigneeUserId { get; set; }
    public string? SuggestedAssigneeName { get; set; }
    public List<string> AcceptanceCriteria { get; set; } = [];
    public List<string> SuggestedSkills { get; set; } = [];
    public List<string> DocumentCitations { get; set; } = [];
    public Guid? DocumentId { get; set; }
    public bool Selected { get; set; } = true;
}

/// <summary>
/// Ứng viên thành viên được Trợ Lý AI xếp hạng kèm chỉ số năng lực thực tế trên Redis trước khi phân công chính thức.
/// </summary>
public sealed class AiQuickAssigneeCandidateDto
{
    public int Rank { get; set; }
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? SeniorityLevel { get; set; }
    public List<string> Roles { get; set; } = [];
    public decimal TotalScore { get; set; }
    public decimal LoadBalanceScore { get; set; }
    public decimal SkillMatchScore { get; set; }
    public decimal HistoryScore { get; set; }
    public decimal CapacityScore { get; set; }
    public int ActiveSprintTasksCount { get; set; }
    public decimal RemainingAvailableHours { get; set; }
    public List<string> Skills { get; set; } = [];
    public string Reason { get; set; } = string.Empty;
    public bool IsRecommended { get; set; }
}

/// <summary>
/// Mục Sub-task đã được người dùng kiểm duyệt/tinh chỉnh trước khi xác nhận áp dụng vào hệ thống.
/// </summary>
public sealed class AiQuickSelectedSubtaskInput
{
    [Required]
    public string TempId { get; set; } = string.Empty;

    public string? Title { get; set; }

    public Guid? AssigneeUserId { get; set; }
}

/// <summary>
/// Yêu cầu xác nhận áp dụng hoặc hủy kế hoạch AI đang lưu tạm trên Redis.
/// </summary>
public sealed class AiQuickActionConfirmRequest
{
    [Required]
    public string ActionId { get; set; } = string.Empty;

    public Guid? ProjectId { get; set; }

    /// <summary>
    /// Chế độ xác nhận: "apply" | "cancel"
    /// </summary>
    [Required]
    public string Mode { get; set; } = "apply";

    public List<AiQuickSelectedSubtaskInput> SelectedSubtasks { get; set; } = [];

    public Guid? SelectedAssigneeUserId { get; set; }

    public string? OverrideReason { get; set; }
}

/// <summary>
/// Kết quả sau khi xác nhận áp dụng hoặc hủy kế hoạch thao tác AI nhanh.
/// </summary>
public sealed class AiQuickActionConfirmResponse
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ActionId { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public int CreatedSubtaskCount { get; set; }
    public Guid? UpdatedIssueId { get; set; }
    public Guid? AssignedUserId { get; set; }
    public string? AssignedUserName { get; set; }
    public List<AiCreatedIssueItemDto> CreatedIssues { get; set; } = [];
}

