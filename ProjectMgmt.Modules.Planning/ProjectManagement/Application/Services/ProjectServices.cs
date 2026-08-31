using ProjectMgmt.IssueTracking.Contracts;
using ProjectMgmt.Modules.Planning.ProjectManagement.Domain.IRepositories;
using ProjectMgmt.ProjectManagement.Contracts;

namespace ProjectMgmt.Modules.Planning.ProjectManagement.Application.Services;

internal class ProjectLookupService : IProjectLookupService
{
    private readonly IProjectRepository _repository;

    public ProjectLookupService(IProjectRepository repository)
    {
        _repository = repository;
    }

    public Task<bool> ExistsAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        _repository.ExistsAsync(projectId, cancellationToken);

    public async Task<string?> GetProjectKeyAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        (await _repository.GetByIdAsync(projectId, cancellationToken))?.ProjectKey;

    public async Task<IReadOnlyList<ProjectIssueTypeDto>> GetIssueTypesAsync(
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        (await _repository.GetIssueTypesAsync(projectId, cancellationToken))
            .Select(x => new ProjectIssueTypeDto(x.Id, x.Name, x.IsSubtask, x.HierarchyLevel))
            .ToList();

    public async Task<IReadOnlyList<ProjectStatusDto>> GetStatusesAsync(
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        (await _repository.GetStatusesAsync(projectId, cancellationToken))
            .Select(x => new ProjectStatusDto(x.Id, x.Name, x.Category, x.IsInitial, x.OrderIndex))
            .ToList();
}

internal class WorkflowValidationService : IWorkflowValidationService
{
    private readonly IProjectRepository _repository;
    private readonly IIssueWorkInProgressCounter _issueCounter;

    public WorkflowValidationService(
        IProjectRepository repository,
        IIssueWorkInProgressCounter issueCounter)
    {
        _repository = repository;
        _issueCounter = issueCounter;
    }

    public async Task<WorkflowValidationResult> CanTransitionAsync(
        Guid projectId,
        Guid fromStatusId,
        Guid toStatusId,
        IReadOnlyCollection<string> userPermissions,
        CancellationToken cancellationToken = default)
    {
        var transition = await _repository.GetTransitionAsync(
            projectId,
            fromStatusId,
            toStatusId,
            cancellationToken);

        if (transition is null)
        {
            return new WorkflowValidationResult(false, "Workflow transition is not configured.");
        }

        if (!string.IsNullOrWhiteSpace(transition.RequiredPermissionCode)
            && !userPermissions.Contains(transition.RequiredPermissionCode, StringComparer.OrdinalIgnoreCase))
        {
            return new WorkflowValidationResult(false, "The required permission is missing.");
        }

        return new WorkflowValidationResult(true);
    }

    public async Task<ProjectStatusDto?> GetInitialStatusAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var status = await _repository.GetInitialStatusAsync(projectId, cancellationToken);
        return status is null
            ? null
            : new ProjectStatusDto(status.Id, status.Name, status.Category, status.IsInitial, status.OrderIndex);
    }

    public async Task<WorkflowValidationResult> CheckWipLimitAsync(
        Guid boardId,
        Guid statusId,
        CancellationToken cancellationToken = default)
    {
        var column = await _repository.GetBoardColumnStateAsync(boardId, statusId, cancellationToken);

        if (column is null)
        {
            return new WorkflowValidationResult(false, "Board column is not configured.");
        }

        if (column.WipLimit is null)
        {
            return new WorkflowValidationResult(true);
        }

        var currentCount = await _issueCounter.CountByStatusAsync(
            column.ProjectId,
            statusId,
            cancellationToken);

        return new WorkflowValidationResult(
            currentCount < column.WipLimit.Value,
            currentCount < column.WipLimit.Value ? null : "The WIP limit has been reached.",
            currentCount,
            column.WipLimit);
    }
}

internal class IssueNumberGenerator : IIssueNumberGenerator
{
    private readonly IProjectRepository _repository;

    public IssueNumberGenerator(IProjectRepository repository)
    {
        _repository = repository;
    }

    public Task<int> NextAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        _repository.ReserveNextIssueNumberAsync(projectId, cancellationToken);
}
