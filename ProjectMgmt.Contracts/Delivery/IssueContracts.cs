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
    Task<IssueSummaryDto?> GetByIdAsync(Guid issueId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IssueSummaryDto>> GetBySprintAsync(Guid sprintId, CancellationToken cancellationToken = default);

    /// <summary>Issue đang mở (chưa có ResolvedAt) giao cho người dùng, mọi Project.</summary>
    Task<IReadOnlyList<IssueSummaryDto>> GetOpenByAssigneeAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IssueStatusChangeDto>> GetStatusHistoryAsync(Guid issueId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IssueAssignmentChangeDto>> GetAssignmentHistoryAsync(Guid issueId, CancellationToken cancellationToken = default);
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
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<Result<int>> ReturnToBacklogAsync(
        Guid projectId,
        IReadOnlyCollection<Guid> issueIds,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}
