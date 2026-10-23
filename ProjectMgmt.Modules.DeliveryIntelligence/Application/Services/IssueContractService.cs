using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Domain.IRepositories;
using ProjectMgmt.BuildingBlocks.Results;
using ProjectMgmt.IssueTracking.Contracts;

namespace DeliveryIntelligence.Application.Services;

/// <summary>Implementation thật của IIssueReadService và IIssueSprintService (SCRUMAI-177).</summary>
public class IssueContractService : IIssueReadService, IIssueSprintService
{
    public const int MaxBatchSize = 500;
    private readonly IIssueRepository _issues;
    private readonly TimeProvider _clock;

    public IssueContractService(IIssueRepository issues, TimeProvider clock)
    {
        _issues = issues;
        _clock = clock;
    }

    public async Task<IssueSummaryDto?> GetByIdAsync(Guid issueId, CancellationToken cancellationToken = default)
    {
        var issue = await _issues.GetByIdAsync(issueId, cancellationToken: cancellationToken);
        return issue is null ? null : ToSummary(issue);
    }

    public async Task<IReadOnlyList<IssueSummaryDto>> GetBySprintAsync(Guid sprintId, CancellationToken cancellationToken = default) =>
        (await _issues.GetBySprintAsync(sprintId, cancellationToken)).Select(ToSummary).ToList();

    public async Task<IReadOnlyList<IssueSummaryDto>> GetOpenByAssigneeAsync(Guid userId, CancellationToken cancellationToken = default) =>
        (await _issues.GetOpenByAssigneeAsync(userId, cancellationToken)).Select(ToSummary).ToList();

    public async Task<IReadOnlyList<IssueStatusChangeDto>> GetStatusHistoryAsync(Guid issueId, CancellationToken cancellationToken = default) =>
        (await _issues.GetStatusHistoryAsync(issueId, cancellationToken))
            .Select(h => new IssueStatusChangeDto(h.IssueId, h.FromStatusId, h.ToStatusId, h.FromCategory, h.ToCategory,
                h.ChangedBy, h.DurationSeconds, h.ChangedAt))
            .ToList();

    public async Task<IReadOnlyList<IssueAssignmentChangeDto>> GetAssignmentHistoryAsync(Guid issueId, CancellationToken cancellationToken = default) =>
        (await _issues.GetAssignmentHistoryAsync(issueId, cancellationToken))
            .Select(h => new IssueAssignmentChangeDto(h.IssueId, h.FromAssigneeId, h.ToAssigneeId, h.AssignedBy,
                h.AssignmentSource, h.AiCandidateId, h.StoryPointsAtTime, h.AssignedAt))
            .ToList();

    public Task<Result<int>> MoveToSprintAsync(
        Guid projectId,
        IReadOnlyCollection<Guid> issueIds,
        Guid sprintId,
        Guid actorUserId,
        CancellationToken cancellationToken = default) =>
        sprintId == Guid.Empty
            ? Task.FromResult(Result<int>.Failure(Error.Validation("ISSUE_SPRINT_INVALID", "Sprint không hợp lệ.")))
            : SetSprintAsync(projectId, issueIds, sprintId, actorUserId, cancellationToken);

    public Task<Result<int>> ReturnToBacklogAsync(
        Guid projectId,
        IReadOnlyCollection<Guid> issueIds,
        Guid actorUserId,
        CancellationToken cancellationToken = default) =>
        SetSprintAsync(projectId, issueIds, null, actorUserId, cancellationToken);

    private async Task<Result<int>> SetSprintAsync(
        Guid projectId,
        IReadOnlyCollection<Guid> issueIds,
        Guid? sprintId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(issueIds);
        if (issueIds.Count is 0 or > MaxBatchSize)
        {
            return Result<int>.Failure(Error.Validation("ISSUE_BATCH_SIZE_INVALID", $"Cần từ 1 đến {MaxBatchSize} Issue."));
        }

        var requested = await _issues.GetByIdsAsync(issueIds, cancellationToken);
        if (requested.Count != issueIds.Distinct().Count() || requested.Any(issue => issue.ProjectId != projectId))
        {
            return Result<int>.Failure(Error.Validation("ISSUE_NOT_IN_PROJECT", "Có Issue không tồn tại hoặc không thuộc Project."));
        }

        // Sub-task đi theo cha: thêm con của các Issue được chọn.
        var affected = requested.ToDictionary(issue => issue.Id);
        foreach (var parent in requested)
        {
            foreach (var child in await _issues.GetChildrenAsync(parent.Id, cancellationToken))
            {
                affected.TryAdd(child.Id, child);
            }
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var changed = 0;
        foreach (var issue in affected.Values.Where(issue => issue.SprintId != sprintId))
        {
            await _issues.AddActivityAsync(
                IssueActivities.Create(issue.Id, actorUserId, IssueActivities.SprintChanged, now,
                    nameof(Issue.SprintId), issue.SprintId, sprintId),
                cancellationToken);
            issue.SprintId = sprintId;
            issue.UpdatedAt = now;
            changed++;
        }

        if (changed > 0)
        {
            await _issues.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(changed);
    }

    private static IssueSummaryDto ToSummary(Issue issue) => new(
        issue.Id, issue.ProjectId, issue.IssueNumber, issue.Title, issue.IssueTypeId, issue.StatusId, issue.PriorityId,
        issue.AssigneeId, issue.SprintId, issue.ParentId, issue.EpicId, issue.StoryPoints, issue.RankOrder, issue.DueDate,
        issue.StartedAt, issue.ResolvedAt);
}
