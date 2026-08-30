using ProjectMgmt.BuildingBlocks.Common;
using ProjectMgmt.BuildingBlocks.Results;

namespace ProjectMgmt.IssueTracking.Contracts;

public sealed record CreateIssueDto(
    Guid ProjectId,
    Guid ReporterId,
    Guid IssueTypeId,
    string Title,
    Guid? SprintId = null,
    Guid? ParentId = null,
    Guid? EpicId = null,
    Guid? AssigneeId = null,
    Guid? PriorityId = null,
    string? Description = null,
    decimal? StoryPoints = null,
    DateOnly? DueDate = null,
    bool IsAiGenerated = false,
    Guid? AiGenerationLogId = null);

public sealed record CreateIssueResultDto(Guid IssueId, int IssueNumber, string IssueKey);

public sealed record IssueDto(
    Guid Id,
    Guid ProjectId,
    int IssueNumber,
    string Title,
    Guid StatusId,
    Guid IssueTypeId,
    Guid? SprintId,
    Guid? AssigneeId,
    decimal? StoryPoints,
    DateOnly? DueDate,
    bool IsAiGenerated);

public sealed record IssueStatusHistoryDto(
    Guid Id,
    Guid IssueId,
    Guid? FromStatusId,
    Guid ToStatusId,
    string? FromCategory,
    string ToCategory,
    Guid? ChangedBy,
    long? DurationSeconds,
    DateTimeOffset ChangedAtUtc);

public sealed record IssueAssignmentHistoryDto(
    Guid Id,
    Guid IssueId,
    Guid? FromAssigneeId,
    Guid? ToAssigneeId,
    string AssignmentSource,
    DateTimeOffset AssignedAtUtc);

public sealed record IssueRequiredSkillDto(
    Guid IssueId,
    Guid SkillId,
    int MinimumLevel,
    decimal Weight,
    string Source);

public interface IIssueService
{
    Task<Result<CreateIssueResultDto>> CreateIssueAsync(
        CreateIssueDto request,
        CancellationToken cancellationToken = default);
}

public interface IIssueReadService
{
    Task<Result<IssueDto>> GetByIdAsync(Guid issueId, CancellationToken cancellationToken = default);

    Task<PagedResult<IssueDto>> GetBySprintAsync(
        Guid sprintId,
        PageRequest page,
        CancellationToken cancellationToken = default);

    Task<PagedResult<IssueDto>> GetOpenByAssigneeAsync(
        Guid assigneeId,
        PageRequest page,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IssueStatusHistoryDto>> GetStatusHistoryAsync(
        Guid issueId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IssueAssignmentHistoryDto>> GetAssignmentHistoryAsync(
        Guid issueId,
        CancellationToken cancellationToken = default);
}

public interface IIssueSprintService
{
    Task<Result> MoveToSprintAsync(
        IReadOnlyCollection<Guid> issueIds,
        Guid sprintId,
        CancellationToken cancellationToken = default);

    Task<Result> ReturnToBacklogAsync(
        IReadOnlyCollection<Guid> issueIds,
        CancellationToken cancellationToken = default);
}

public interface IIssueSkillService
{
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<IssueRequiredSkillDto>>> GetRequiredSkillsAsync(
        IReadOnlyCollection<Guid> issueIds,
        CancellationToken cancellationToken = default);
}
