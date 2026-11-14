using ProjectMgmt.BuildingBlocks.Results;

namespace ProjectMgmt.IssueTracking.Contracts;

/// <summary>Ảnh chụp Issue cho consumer A/B (không lộ entity EF). Thời gian là UTC.</summary>
public record IssueSummaryDto(
    Guid Id,
    Guid ProjectId,
    int IssueNumber,
    string Title,
    Guid IssueTypeId,
    Guid StatusId,
    Guid? PriorityId,
    Guid? AssigneeId,
    Guid? SprintId,
    Guid? ParentId,
    Guid? EpicId,
    decimal? StoryPoints,
    decimal RankOrder,
    DateOnly? DueDate,
    DateTime? StartedAt,
    DateTime? ResolvedAt);

public record IssueStatusChangeDto(
    Guid IssueId,
    Guid? FromStatusId,
    Guid ToStatusId,
    string? FromCategory,
    string ToCategory,
    Guid? ChangedBy,
    long? DurationSeconds,
    DateTime ChangedAt);

public record IssueAssignmentChangeDto(
    Guid IssueId,
    Guid? FromAssigneeId,
    Guid? ToAssigneeId,
    Guid? AssignedBy,
    string AssignmentSource,
    Guid? AiCandidateId,
    decimal? StoryPointsAtTime,
    DateTime AssignedAt);

/// <summary>DeliveryIntelligence (C) cung cấp; Planning/AI đọc Issue qua DTO, không đọc DbContext của C.</summary>
public interface IIssueReadService
{
    Task<IssueSummaryDto?> GetByIdAsync(Guid issueId);

    Task<IReadOnlyList<IssueSummaryDto>> GetBySprintAsync(Guid sprintId);

    /// <summary>Issue đang mở (chưa có ResolvedAt) giao cho người dùng, mọi Project.</summary>
    Task<IReadOnlyList<IssueSummaryDto>> GetOpenByAssigneeAsync(Guid userId);

    Task<IReadOnlyList<IssueStatusChangeDto>> GetStatusHistoryAsync(Guid issueId);

    Task<IReadOnlyList<IssueAssignmentChangeDto>> GetAssignmentHistoryAsync(Guid issueId);
}

/// <summary>
/// Planning (B) điều phối Sprint và tự kiểm Sprint thuộc Project/còn mở trước khi gọi.
/// C chỉ đổi SprintId của Issue (kèm Sub-task) và ghi ActivityLog. Trả về số Issue thực sự thay đổi.
/// </summary>
public interface IIssueSprintService
{
    Task<Result<int>> MoveToSprintAsync(
        Guid projectId,
        IReadOnlyCollection<Guid> issueIds,
        Guid sprintId,
        Guid actorUserId);

    Task<Result<int>> ReturnToBacklogAsync(
        Guid projectId,
        IReadOnlyCollection<Guid> issueIds,
        Guid actorUserId);
}

public record IssueRequiredSkillDto(Guid IssueId, Guid SkillId, int MinLevel, decimal Weight, string Source);

/// <summary>Kỹ năng yêu cầu của Issue cho AI phân công (B) — đọc theo lô, không lộ entity.</summary>
public interface IIssueSkillService
{
    Task<IReadOnlyList<IssueRequiredSkillDto>> GetRequiredSkillsAsync(IEnumerable<Guid> issueIds);
}
