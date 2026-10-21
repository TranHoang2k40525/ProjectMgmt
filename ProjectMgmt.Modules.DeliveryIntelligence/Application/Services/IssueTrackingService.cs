using DeliveryIntelligence.Application.Dto;
using DeliveryIntelligence.Application.IServices;
using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Domain.IRepositories;
using FluentValidation;
using FluentValidation.Results;
using ProjectMgmt.BuildingBlocks.Results;
using ProjectMgmt.IdentityAccess.Contracts;

namespace DeliveryIntelligence.Application.Services;

public class IssueTrackingService : IIssueTrackingService
{
    private readonly IIssueRepository _issues;
    private readonly IPermissionEvaluator _permissions;
    private readonly TimeProvider _clock;
    private readonly IssueHistoryRecorder _history;
    private readonly IValidator<CreateIssueDto> _createValidator;
    private readonly IValidator<UpdateIssueDto> _updateValidator;
    private readonly IValidator<IssueListQueryDto> _listValidator;

    public IssueTrackingService(
        IIssueRepository issues,
        IPermissionEvaluator permissions,
        TimeProvider clock,
        IssueHistoryRecorder history,
        IValidator<CreateIssueDto> createValidator,
        IValidator<UpdateIssueDto> updateValidator,
        IValidator<IssueListQueryDto> listValidator)
    {
        _issues = issues;
        _permissions = permissions;
        _clock = clock;
        _history = history;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _listValidator = listValidator;
    }

    public async Task<Result<IssueDetailDto>> GetAsync(Guid issueId, CancellationToken cancellationToken = default)
    {
        var issue = await _issues.GetByIdAsync(issueId, cancellationToken: cancellationToken);
        return issue is null
            ? Result<IssueDetailDto>.Failure(IssueErrors.NotFound)
            : Result.Success(IssueMapper.ToDetail(issue));
    }

    public async Task<Result<PagedResultDto<IssueListItemDto>>> ListAsync(
        Guid projectId,
        IssueListQueryDto query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (projectId == Guid.Empty)
        {
            return Result<PagedResultDto<IssueListItemDto>>.Failure(
                Error.Validation("ISSUE_PROJECT_REQUIRED", "Thiếu Project."));
        }

        var validation = _listValidator.Validate(query);
        if (!validation.IsValid)
        {
            return Result<PagedResultDto<IssueListItemDto>>.Failure(ToError(validation));
        }

        var page = await _issues.ListAsync(IssueMapper.ToFilter(projectId, query), cancellationToken);
        return Result.Success(new PagedResultDto<IssueListItemDto>
        {
            Items = page.Items.Select(issue => IssueMapper.ToListItem(issue)).ToList(),
            TotalCount = page.TotalCount,
            Page = query.Page,
            PageSize = query.PageSize
        });
    }

    public async Task<Result<IssueDetailDto>> UpdateAsync(
        Guid issueId,
        UpdateIssueDto request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateUpdate(request);
        if (validation.IsFailure)
        {
            return Result<IssueDetailDto>.Failure(validation.Error);
        }

        var issue = await _issues.GetByIdAsync(issueId, cancellationToken: cancellationToken);
        if (issue is null)
        {
            return Result<IssueDetailDto>.Failure(IssueErrors.NotFound);
        }

        if (request.AssigneeId.HasValue
            && request.AssigneeId != issue.AssigneeId
            && !await _permissions.IsProjectMemberAsync(request.AssigneeId.Value, issue.ProjectId))
        {
            return Result<IssueDetailDto>.Failure(IssueErrors.AssigneeNotMember);
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var changes = IssueActivities.Changes(issue.Id, actorUserId, now,
            (nameof(Issue.Title), issue.Title, request.Title.Trim()),
            (nameof(Issue.Description), issue.Description, request.Description),
            (nameof(Issue.PriorityId), issue.PriorityId, request.PriorityId),
            (nameof(Issue.AssigneeId), issue.AssigneeId, request.AssigneeId),
            (nameof(Issue.StoryPoints), issue.StoryPoints, request.StoryPoints),
            (nameof(Issue.OriginalEstimateMinutes), issue.OriginalEstimateMinutes, request.OriginalEstimateMinutes),
            (nameof(Issue.DueDate), issue.DueDate, request.DueDate)).ToList();
        if (changes.Count == 0)
        {
            return Result.Success(IssueMapper.ToDetail(issue));
        }

        var previousAssignee = issue.AssigneeId;
        IssueMapper.ApplyUpdate(issue, request, now);
        await _history.RecordAssignmentAsync(issue, previousAssignee, issue.AssigneeId, actorUserId, now, cancellationToken: cancellationToken);
        foreach (var change in changes)
        {
            await _issues.AddActivityAsync(change, cancellationToken);
        }

        await _issues.SaveChangesAsync(cancellationToken);
        return Result.Success(IssueMapper.ToDetail(issue));
    }

    public async Task<Result> DeleteAsync(Guid issueId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var issue = await _issues.GetByIdAsync(issueId, cancellationToken: cancellationToken);
        if (issue is null)
        {
            return Result.Failure(IssueErrors.NotFound);
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var removed = new List<Issue> { issue };
        for (var index = 0; index < removed.Count; index++)
        {
            foreach (var child in await _issues.GetChildrenAsync(removed[index].Id, cancellationToken))
            {
                if (removed.TrueForAll(existing => existing.Id != child.Id))
                {
                    removed.Add(child);
                }
            }
        }

        foreach (var item in removed)
        {
            item.IsDeleted = true;
            item.UpdatedAt = now;
            await _issues.AddActivityAsync(
                IssueActivities.Create(item.Id, actorUserId, IssueActivities.Deleted, now,
                    detail: item.Id == issue.Id ? null : new { deletedWithIssueId = issue.Id }),
                cancellationToken);
        }

        foreach (var linked in await _issues.GetByEpicAsync(issue.Id, cancellationToken))
        {
            linked.EpicId = null;
        }

        await _issues.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public Result ValidateCreate(CreateIssueDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = _createValidator.Validate(request);
        return validation.IsValid ? Result.Success() : Result.Failure(ToError(validation));
    }

    public Result ValidateUpdate(UpdateIssueDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = _updateValidator.Validate(request);
        return validation.IsValid ? Result.Success() : Result.Failure(ToError(validation));
    }

    internal static Error ToError(ValidationResult validation)
    {
        var first = validation.Errors[0];
        return Error.Validation(first.ErrorCode, first.ErrorMessage);
    }
}

public static class IssueErrors
{
    public static readonly Error NotFound = Error.NotFound("ISSUE_NOT_FOUND", "Không tìm thấy Issue.");
    public static readonly Error AssigneeNotMember = Error.Validation("ISSUE_ASSIGNEE_NOT_MEMBER", "Người xử lý chưa là thành viên Project.");
    public static readonly Error ReporterNotMember = Error.Forbidden("ISSUE_REPORTER_NOT_MEMBER", "Người tạo không thuộc Project.");
    public static readonly Error ProjectNotFound = Error.NotFound("ISSUE_PROJECT_NOT_FOUND", "Project không tồn tại hoặc đã lưu trữ.");
    public static readonly Error TypeInvalid = Error.Validation("ISSUE_TYPE_INVALID", "Loại Issue không thuộc Project.");
    public static readonly Error InitialStatusMissing = Error.Conflict("ISSUE_INITIAL_STATUS_MISSING", "Workflow của Project chưa có trạng thái khởi tạo.");

    public static Error FromRule(DeliveryIntelligence.Domain.UseCases.IssueRuleViolation violation) =>
        Error.Validation(violation.Code, violation.Message);
}
