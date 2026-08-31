using ProjectMgmt.BuildingBlocks.Common;
using ProjectMgmt.BuildingBlocks.Results;
using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.IssueTracking.Contracts;
using ProjectMgmt.Modules.DeliveryIntelligence.IssueTracking.Domain.Entities;
using ProjectMgmt.Modules.DeliveryIntelligence.IssueTracking.Domain.Repositories;
using ProjectMgmt.ProjectManagement.Contracts;

namespace ProjectMgmt.Modules.DeliveryIntelligence.IssueTracking.Application.Services;

internal sealed class IssueService(
    IIssueRepository repository,
    IUserLookupService users,
    IProjectLookupService projects,
    ISprintLookupService sprints,
    IWorkflowValidationService workflow,
    IIssueNumberGenerator numberGenerator) : IIssueService
{
    public async Task<Result<CreateIssueResultDto>> CreateIssueAsync(
        CreateIssueDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.ProjectId == Guid.Empty || request.ReporterId == Guid.Empty || request.IssueTypeId == Guid.Empty)
        {
            return Result<CreateIssueResultDto>.Failure(
                Error.Validation("issue.required_ids", "Project, reporter and issue type are required."));
        }

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 500)
        {
            return Result<CreateIssueResultDto>.Failure(
                Error.Validation("issue.title", "Title is required and cannot exceed 500 characters."));
        }

        if (request.StoryPoints < 0)
        {
            return Result<CreateIssueResultDto>.Failure(
                Error.Validation("issue.story_points", "Story points cannot be negative."));
        }

        if (request.IsAiGenerated && request.AiGenerationLogId is null)
        {
            return Result<CreateIssueResultDto>.Failure(
                Error.Validation("issue.ai_log", "An AI-generated issue must reference its generation log."));
        }

        if (!await projects.ExistsAsync(request.ProjectId, cancellationToken))
        {
            return Result<CreateIssueResultDto>.Failure(Error.NotFound("project.not_found", "Project was not found."));
        }

        if (!await users.ExistsAsync(request.ReporterId, cancellationToken)
            || request.AssigneeId is { } assigneeId && !await users.ExistsAsync(assigneeId, cancellationToken))
        {
            return Result<CreateIssueResultDto>.Failure(Error.NotFound("user.not_found", "Reporter or assignee was not found."));
        }

        var issueTypes = await projects.GetIssueTypesAsync(request.ProjectId, cancellationToken);
        if (issueTypes.All(x => x.Id != request.IssueTypeId))
        {
            return Result<CreateIssueResultDto>.Failure(
                Error.Validation("issue.type", "Issue type does not belong to the project."));
        }

        if (request.SprintId is { } sprintId)
        {
            var sprint = await sprints.GetByIdAsync(sprintId, cancellationToken);
            if (sprint is null || sprint.ProjectId != request.ProjectId || sprint.Status == "Completed")
            {
                return Result<CreateIssueResultDto>.Failure(
                    Error.Validation("issue.sprint", "Sprint is invalid for this project."));
            }
        }

        foreach (var relatedId in new[] { request.ParentId, request.EpicId }.OfType<Guid>())
        {
            var related = await repository.GetByIdAsync(relatedId, cancellationToken);
            if (related is null || related.ProjectId != request.ProjectId)
            {
                return Result<CreateIssueResultDto>.Failure(
                    Error.Validation("issue.hierarchy", "Parent and epic must belong to the same project."));
            }
        }

        var initialStatus = await workflow.GetInitialStatusAsync(request.ProjectId, cancellationToken);
        if (initialStatus is null)
        {
            return Result<CreateIssueResultDto>.Failure(
                Error.Conflict("workflow.initial_status_missing", "The project has no initial workflow status."));
        }

        var projectKey = await projects.GetProjectKeyAsync(request.ProjectId, cancellationToken);
        if (projectKey is null)
        {
            return Result<CreateIssueResultDto>.Failure(Error.NotFound("project.not_found", "Project was not found."));
        }

        var issueNumber = await numberGenerator.NextAsync(request.ProjectId, cancellationToken);
        var now = DateTime.UtcNow;
        var issue = new Issue
        {
            Id = Guid.NewGuid(),
            ProjectId = request.ProjectId,
            IssueNumber = issueNumber,
            SprintId = request.SprintId,
            ParentId = request.ParentId,
            EpicId = request.EpicId,
            AssigneeId = request.AssigneeId,
            ReporterId = request.ReporterId,
            StatusId = initialStatus.Id,
            IssueTypeId = request.IssueTypeId,
            PriorityId = request.PriorityId,
            Title = request.Title.Trim(),
            Description = request.Description,
            StoryPoints = request.StoryPoints,
            RankOrder = await repository.GetNextRankAsync(request.ProjectId, request.SprintId, cancellationToken),
            DueDate = request.DueDate,
            IsAiGenerated = request.IsAiGenerated,
            AiGenerationLogId = request.AiGenerationLogId,
            IsAiAssigned = false,
            CreatedAt = now
        };

        await repository.AddAsync(issue, cancellationToken);
        await repository.AddStatusHistoryAsync(new IssueStatusHistory
        {
            Id = Guid.NewGuid(),
            IssueId = issue.Id,
            ToStatusId = initialStatus.Id,
            ToCategory = initialStatus.Category,
            ChangedBy = request.ReporterId,
            ChangedAt = now
        }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return Result<CreateIssueResultDto>.Success(
            new CreateIssueResultDto(issue.Id, issueNumber, $"{projectKey}-{issueNumber}"));
    }
}

internal sealed class IssueReadService(IIssueRepository repository) : IIssueReadService
{
    public async Task<Result<IssueDto>> GetByIdAsync(Guid issueId, CancellationToken cancellationToken = default)
    {
        var issue = await repository.GetByIdAsync(issueId, cancellationToken);
        return issue is null
            ? Result<IssueDto>.Failure(Error.NotFound("issue.not_found", "Issue was not found."))
            : Result<IssueDto>.Success(ToDto(issue));
    }

    public async Task<PagedResult<IssueDto>> GetBySprintAsync(
        Guid sprintId,
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        var rows = await repository.GetBySprintAsync(sprintId, page.Skip, page.PageSize, cancellationToken);
        var count = await repository.CountBySprintAsync(sprintId, cancellationToken);
        return new PagedResult<IssueDto>(rows.Select(ToDto).ToList(), page.PageNumber, page.PageSize, count);
    }

    public async Task<PagedResult<IssueDto>> GetOpenByAssigneeAsync(
        Guid assigneeId,
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        var rows = await repository.GetOpenByAssigneeAsync(assigneeId, page.Skip, page.PageSize, cancellationToken);
        var count = await repository.CountOpenByAssigneeAsync(assigneeId, cancellationToken);
        return new PagedResult<IssueDto>(rows.Select(ToDto).ToList(), page.PageNumber, page.PageSize, count);
    }

    public async Task<IReadOnlyList<IssueStatusHistoryDto>> GetStatusHistoryAsync(
        Guid issueId,
        CancellationToken cancellationToken = default) =>
        (await repository.GetStatusHistoryAsync(issueId, cancellationToken))
            .Select(x => new IssueStatusHistoryDto(
                x.Id, x.IssueId, x.FromStatusId, x.ToStatusId, x.FromCategory, x.ToCategory,
                x.ChangedBy, x.DurationSeconds, AsUtc(x.ChangedAt)))
            .ToList();

    public async Task<IReadOnlyList<IssueAssignmentHistoryDto>> GetAssignmentHistoryAsync(
        Guid issueId,
        CancellationToken cancellationToken = default) =>
        (await repository.GetAssignmentHistoryAsync(issueId, cancellationToken))
            .Select(x => new IssueAssignmentHistoryDto(
                x.Id, x.IssueId, x.FromAssigneeId, x.ToAssigneeId, x.AssignmentSource, AsUtc(x.AssignedAt)))
            .ToList();

    private static IssueDto ToDto(Issue issue) => new(
        issue.Id, issue.ProjectId, issue.IssueNumber, issue.Title, issue.StatusId, issue.IssueTypeId,
        issue.SprintId, issue.AssigneeId, issue.StoryPoints, issue.DueDate, issue.IsAiGenerated);

    private static DateTimeOffset AsUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}

internal sealed class IssueSprintService(IIssueRepository repository, ISprintLookupService sprints) : IIssueSprintService
{
    public async Task<Result> MoveToSprintAsync(
        IReadOnlyCollection<Guid> issueIds,
        Guid sprintId,
        CancellationToken cancellationToken = default)
    {
        var sprint = await sprints.GetByIdAsync(sprintId, cancellationToken);
        if (sprint is null || sprint.Status == "Completed")
        {
            return Result.Failure(Error.Validation("sprint.invalid", "Target sprint does not exist or is completed."));
        }

        var ids = issueIds.Distinct().ToArray();
        var issues = await repository.GetByIdsAsync(ids, cancellationToken);
        if (issues.Count != ids.Length)
        {
            return Result.Failure(Error.NotFound("issue.not_found", "One or more issues were not found."));
        }

        if (issues.Any(x => x.ProjectId != sprint.ProjectId))
        {
            return Result.Failure(Error.Validation("sprint.project_mismatch", "All issues must belong to the sprint project."));
        }

        var nextRank = await repository.GetNextRankAsync(sprint.ProjectId, sprintId, cancellationToken);
        foreach (var issue in issues.OrderBy(x => x.RankOrder))
        {
            issue.SprintId = sprintId;
            issue.RankOrder = nextRank;
            nextRank += 1000m;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ReturnToBacklogAsync(
        IReadOnlyCollection<Guid> issueIds,
        CancellationToken cancellationToken = default)
    {
        var ids = issueIds.Distinct().ToArray();
        var issues = await repository.GetByIdsAsync(ids, cancellationToken);
        if (issues.Count != ids.Length)
        {
            return Result.Failure(Error.NotFound("issue.not_found", "One or more issues were not found."));
        }

        foreach (var projectGroup in issues.GroupBy(x => x.ProjectId))
        {
            var nextRank = await repository.GetNextRankAsync(projectGroup.Key, null, cancellationToken);
            foreach (var issue in projectGroup.OrderBy(x => x.RankOrder))
            {
                issue.SprintId = null;
                issue.RankOrder = nextRank;
                nextRank += 1000m;
            }
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class IssueSkillService(IIssueRepository repository) : IIssueSkillService
{
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<IssueRequiredSkillDto>>> GetRequiredSkillsAsync(
        IReadOnlyCollection<Guid> issueIds,
        CancellationToken cancellationToken = default) =>
        (await repository.GetRequiredSkillsAsync(issueIds, cancellationToken))
            .GroupBy(x => x.IssueId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<IssueRequiredSkillDto>)group
                    .Select(x => new IssueRequiredSkillDto(x.IssueId, x.SkillId, x.MinLevel, x.Weight, x.Source))
                    .ToList());
}

internal sealed class IssueWorkInProgressCounter(IIssueRepository repository) : IIssueWorkInProgressCounter
{
    public Task<int> CountByStatusAsync(
        Guid projectId,
        Guid statusId,
        CancellationToken cancellationToken = default) =>
        repository.CountByStatusAsync(projectId, statusId, cancellationToken);
}
