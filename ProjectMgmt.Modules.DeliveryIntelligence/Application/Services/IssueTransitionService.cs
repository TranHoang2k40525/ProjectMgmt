using DeliveryIntelligence.Application.Dto;
using DeliveryIntelligence.Application.IServices;
using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Domain.IRepositories;
using DeliveryIntelligence.Domain.UseCases;
using ProjectMgmt.BuildingBlocks.Results;
using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.ProjectManagement.Contracts;

namespace DeliveryIntelligence.Application.Services;

public class IssueTransitionService : IIssueTransitionService
{
    private const int CommentMaxLength = 20_000;
    private readonly IIssueRepository _issues;
    private readonly ICollaborationRepository _collaboration;
    private readonly IProjectLookupService _projects;
    private readonly IWorkflowValidationService _workflow;
    private readonly IIssueNotifier _notifier;
    private readonly IPermissionEvaluator _permissions;
    private readonly IssueHistoryRecorder _history;
    private readonly TimeProvider _clock;

    public IssueTransitionService(
        IIssueRepository issues,
        ICollaborationRepository collaboration,
        IProjectLookupService projects,
        IWorkflowValidationService workflow,
        IIssueNotifier notifier,
        IPermissionEvaluator permissions,
        IssueHistoryRecorder history,
        TimeProvider clock)
    {
        _issues = issues;
        _collaboration = collaboration;
        _projects = projects;
        _workflow = workflow;
        _notifier = notifier;
        _permissions = permissions;
        _history = history;
        _clock = clock;
    }

    public async Task<Result<WorkflowTransitionCheck>> CheckAsync(Guid issueId, Guid targetStatusId, CancellationToken cancellationToken = default)
    {
        var issue = await _issues.GetByIdAsync(issueId, cancellationToken: cancellationToken);
        if (issue is null)
        {
            return Result<WorkflowTransitionCheck>.Failure(IssueErrors.NotFound);
        }

        return Result.Success(await _workflow.CheckTransitionAsync(issue.ProjectId, issue.StatusId, targetStatusId, cancellationToken));
    }

    public async Task<Result<IssueTransitionResultDto>> TransitionAsync(
        Guid issueId,
        TransitionIssueDto request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TargetStatusId == Guid.Empty)
        {
            return Result<IssueTransitionResultDto>.Failure(TransitionErrors.TargetRequired);
        }

        if (request.Comment is { Length: > CommentMaxLength })
        {
            return Result<IssueTransitionResultDto>.Failure(Error.Validation("COMMENT_CONTENT_TOO_LONG", "Bình luận quá dài."));
        }

        // Ba DbContext dùng chung một MySqlConnection: mọi lời gọi contract sang Planning phải xong TRƯỚC khi mở transaction.
        var observed = await _issues.GetByIdAsync(issueId, cancellationToken: cancellationToken);
        if (observed is null)
        {
            return Result<IssueTransitionResultDto>.Failure(IssueErrors.NotFound);
        }

        if (request.ExpectedStatusId.HasValue && request.ExpectedStatusId != observed.StatusId)
        {
            return Result<IssueTransitionResultDto>.Failure(TransitionErrors.Stale);
        }

        var fromStatusId = observed.StatusId;
        if (request.TargetStatusId == fromStatusId)
        {
            return Result.Success(ToResult(observed, fromStatusId, changed: false));
        }

        var statuses = (await _projects.GetStatusesAsync(observed.ProjectId, cancellationToken)).ToDictionary(status => status.Id);
        if (!statuses.TryGetValue(request.TargetStatusId, out var target))
        {
            return Result<IssueTransitionResultDto>.Failure(TransitionErrors.TargetNotInProject);
        }

        var check = await _workflow.CheckTransitionAsync(observed.ProjectId, fromStatusId, target.Id, cancellationToken);
        if (!check.Allowed)
        {
            return Result<IssueTransitionResultDto>.Failure(Error.Conflict(
                check.ReasonCode ?? "WORKFLOW_TRANSITION_NOT_ALLOWED",
                check.Reason ?? "Workflow không cho phép chuyển trạng thái này."));
        }

        var plan = new TransitionPlan(fromStatusId, statuses.GetValueOrDefault(fromStatusId)?.Name, target, check.FromCategory);
        var outcome = await _issues.ExecuteInTransactionAsync(ct => ApplyLockedAsync(issueId, plan, request.Comment, actorUserId, ct), cancellationToken);
        if (outcome.IsSuccess && outcome.Value.Changed)
        {
            await NotifyAsync(outcome.Value, target.Name, actorUserId, cancellationToken);
        }

        return outcome;
    }

    private sealed record TransitionPlan(Guid FromStatusId, string? FromName, ProjectStatusDto Target, string? FromCategory);

    /// <summary>Chỉ đụng dữ liệu của module C: khóa dòng Issue, xác nhận trạng thái chưa bị đổi kể từ lúc kiểm, rồi ghi.</summary>
    private async Task<Result<IssueTransitionResultDto>> ApplyLockedAsync(
        Guid issueId,
        TransitionPlan plan,
        string? comment,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var issue = await _issues.LockForUpdateAsync(issueId, cancellationToken);
        if (issue is null)
        {
            return Result<IssueTransitionResultDto>.Failure(IssueErrors.NotFound);
        }

        if (issue.StatusId != plan.FromStatusId)
        {
            return Result<IssueTransitionResultDto>.Failure(TransitionErrors.Stale);
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        IssueStatusRules.Apply(issue, plan.Target.Category, now);
        await _history.RecordStatusAsync(issue, plan.FromStatusId, plan.FromCategory, plan.Target.Id, plan.Target.Category, actorUserId, now, cancellationToken);
        issue.StatusId = plan.Target.Id;
        issue.UpdatedAt = now;
        await _issues.AddActivityAsync(
            IssueActivities.Create(issue.Id, actorUserId, IssueActivities.StatusChanged, now, nameof(Issue.StatusId),
                plan.FromName ?? plan.FromStatusId.ToString(), plan.Target.Name,
                detail: new { fromStatusId = plan.FromStatusId, toStatusId = plan.Target.Id, fromCategory = plan.FromCategory, toCategory = plan.Target.Category }),
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(comment))
        {
            await _collaboration.AddCommentAsync(new Comment
            {
                Id = Guid.NewGuid(),
                IssueId = issue.Id,
                UserId = actorUserId,
                Content = comment.Trim(),
                CreatedAt = now
            }, cancellationToken);
        }

        await _issues.SaveChangesAsync(cancellationToken);
        return Result.Success(ToResult(issue, plan.FromStatusId, changed: true));
    }

    private async Task NotifyAsync(IssueTransitionResultDto result, string statusName, Guid actorUserId, CancellationToken cancellationToken)
    {
        var issue = await _issues.GetByIdAsync(result.IssueId, cancellationToken: cancellationToken);
        if (issue is null)
        {
            return;
        }

        var recipients = await IssueRecipients.CollectAsync(issue, actorUserId, _collaboration, _permissions, cancellationToken);
        if (recipients.Count == 0)
        {
            return;
        }

        await _notifier.NotifyAsync(new IssueNotification(
            issue.Id, issue.ProjectId, actorUserId, IssueNotificationTypes.StatusChanged,
            $"Issue #{issue.IssueNumber} chuyển sang {statusName}", issue.Title, recipients), cancellationToken);
    }

    private static IssueTransitionResultDto ToResult(Issue issue, Guid previousStatusId, bool changed) => new()
    {
        IssueId = issue.Id,
        PreviousStatusId = previousStatusId,
        NewStatusId = issue.StatusId,
        Changed = changed,
        StartedAt = issue.StartedAt,
        ResolvedAt = issue.ResolvedAt
    };
}

/// <summary>
/// Người nhận thông báo của một Issue: người xử lý, người tạo, người theo dõi — trừ người thao tác,
/// và chỉ giữ người CÒN là thành viên Project (watcher cũ đã bị gỡ khỏi Project không được nhận nội dung Issue).
/// </summary>
public static class IssueRecipients
{
    public static async Task<IReadOnlyCollection<Guid>> CollectAsync(
        Issue issue,
        Guid actorUserId,
        ICollaborationRepository collaboration,
        IPermissionEvaluator permissions,
        CancellationToken cancellationToken)
    {
        var recipients = new HashSet<Guid> { issue.ReporterId };
        if (issue.AssigneeId.HasValue)
        {
            recipients.Add(issue.AssigneeId.Value);
        }

        foreach (var watcher in await collaboration.GetWatchersAsync(issue.Id, cancellationToken))
        {
            recipients.Add(watcher.UserId);
        }

        recipients.Remove(actorUserId);
        recipients.Remove(Guid.Empty);
        var members = new List<Guid>();
        foreach (var userId in recipients)
        {
            if (await permissions.IsProjectMemberAsync(userId, issue.ProjectId))
            {
                members.Add(userId);
            }
        }

        return members;
    }
}

public static class TransitionErrors
{
    public static readonly Error TargetRequired = Error.Validation("ISSUE_TRANSITION_TARGET_REQUIRED", "Thiếu trạng thái đích.");
    public static readonly Error TargetNotInProject = Error.Validation("ISSUE_STATUS_NOT_IN_PROJECT", "Trạng thái đích không thuộc workflow của Project.");
    public static readonly Error Stale = Error.Conflict("ISSUE_STATUS_STALE", "Trạng thái Issue vừa được người khác thay đổi, hãy tải lại.");
}
