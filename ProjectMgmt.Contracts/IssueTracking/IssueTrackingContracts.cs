using ProjectMgmt.BuildingBlocks.Common;
using ProjectMgmt.BuildingBlocks.Results;

namespace ProjectMgmt.IssueTracking.Contracts;

public class CreateIssueDto
{
    public CreateIssueDto(
        Guid projectId,
        Guid reporterId,
        Guid issueTypeId,
        string title,
        Guid? sprintId = null,
        Guid? parentId = null,
        Guid? epicId = null,
        Guid? assigneeId = null,
        Guid? priorityId = null,
        string? description = null,
        decimal? storyPoints = null,
        DateOnly? dueDate = null,
        bool isAiGenerated = false,
        Guid? aiGenerationLogId = null)
    {
        ProjectId = projectId;
        ReporterId = reporterId;
        IssueTypeId = issueTypeId;
        Title = title;
        SprintId = sprintId;
        ParentId = parentId;
        EpicId = epicId;
        AssigneeId = assigneeId;
        PriorityId = priorityId;
        Description = description;
        StoryPoints = storyPoints;
        DueDate = dueDate;
        IsAiGenerated = isAiGenerated;
        AiGenerationLogId = aiGenerationLogId;
    }

    public Guid ProjectId { get; set; }
    public Guid ReporterId { get; set; }
    public Guid IssueTypeId { get; set; }
    public string Title { get; set; }
    public Guid? SprintId { get; set; }
    public Guid? ParentId { get; set; }
    public Guid? EpicId { get; set; }
    public Guid? AssigneeId { get; set; }
    public Guid? PriorityId { get; set; }
    public string? Description { get; set; }
    public decimal? StoryPoints { get; set; }
    public DateOnly? DueDate { get; set; }
    public bool IsAiGenerated { get; set; }
    public Guid? AiGenerationLogId { get; set; }
}

public class CreateIssueResultDto
{
    public CreateIssueResultDto(Guid issueId, int issueNumber, string issueKey)
    {
        IssueId = issueId;
        IssueNumber = issueNumber;
        IssueKey = issueKey;
    }

    public Guid IssueId { get; set; }
    public int IssueNumber { get; set; }
    public string IssueKey { get; set; }
}

public class IssueDto
{
    public IssueDto(
        Guid id,
        Guid projectId,
        int issueNumber,
        string title,
        Guid statusId,
        Guid issueTypeId,
        Guid? sprintId,
        Guid? assigneeId,
        decimal? storyPoints,
        DateOnly? dueDate,
        bool isAiGenerated)
    {
        Id = id;
        ProjectId = projectId;
        IssueNumber = issueNumber;
        Title = title;
        StatusId = statusId;
        IssueTypeId = issueTypeId;
        SprintId = sprintId;
        AssigneeId = assigneeId;
        StoryPoints = storyPoints;
        DueDate = dueDate;
        IsAiGenerated = isAiGenerated;
    }

    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public int IssueNumber { get; set; }
    public string Title { get; set; }
    public Guid StatusId { get; set; }
    public Guid IssueTypeId { get; set; }
    public Guid? SprintId { get; set; }
    public Guid? AssigneeId { get; set; }
    public decimal? StoryPoints { get; set; }
    public DateOnly? DueDate { get; set; }
    public bool IsAiGenerated { get; set; }
}

public class IssueStatusHistoryDto
{
    public IssueStatusHistoryDto(
        Guid id,
        Guid issueId,
        Guid? fromStatusId,
        Guid toStatusId,
        string? fromCategory,
        string toCategory,
        Guid? changedBy,
        long? durationSeconds,
        DateTimeOffset changedAtUtc)
    {
        Id = id;
        IssueId = issueId;
        FromStatusId = fromStatusId;
        ToStatusId = toStatusId;
        FromCategory = fromCategory;
        ToCategory = toCategory;
        ChangedBy = changedBy;
        DurationSeconds = durationSeconds;
        ChangedAtUtc = changedAtUtc;
    }

    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public Guid? FromStatusId { get; set; }
    public Guid ToStatusId { get; set; }
    public string? FromCategory { get; set; }
    public string ToCategory { get; set; }
    public Guid? ChangedBy { get; set; }
    public long? DurationSeconds { get; set; }
    public DateTimeOffset ChangedAtUtc { get; set; }
}

public class IssueAssignmentHistoryDto
{
    public IssueAssignmentHistoryDto(
        Guid id,
        Guid issueId,
        Guid? fromAssigneeId,
        Guid? toAssigneeId,
        string assignmentSource,
        DateTimeOffset assignedAtUtc)
    {
        Id = id;
        IssueId = issueId;
        FromAssigneeId = fromAssigneeId;
        ToAssigneeId = toAssigneeId;
        AssignmentSource = assignmentSource;
        AssignedAtUtc = assignedAtUtc;
    }

    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public Guid? FromAssigneeId { get; set; }
    public Guid? ToAssigneeId { get; set; }
    public string AssignmentSource { get; set; }
    public DateTimeOffset AssignedAtUtc { get; set; }
}

public class IssueRequiredSkillDto
{
    public IssueRequiredSkillDto(
        Guid issueId,
        Guid skillId,
        int minimumLevel,
        decimal weight,
        string source)
    {
        IssueId = issueId;
        SkillId = skillId;
        MinimumLevel = minimumLevel;
        Weight = weight;
        Source = source;
    }

    public Guid IssueId { get; set; }
    public Guid SkillId { get; set; }
    public int MinimumLevel { get; set; }
    public decimal Weight { get; set; }
    public string Source { get; set; }
}

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

public interface IIssueWorkInProgressCounter
{
    Task<int> CountByStatusAsync(
        Guid projectId,
        Guid statusId,
        CancellationToken cancellationToken = default);
}
