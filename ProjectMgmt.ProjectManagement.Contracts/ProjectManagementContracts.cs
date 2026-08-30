namespace ProjectMgmt.ProjectManagement.Contracts;

public sealed record ProjectIssueTypeDto(
    Guid Id,
    string Name,
    bool IsSubtask,
    int HierarchyLevel);

public sealed record ProjectStatusDto(
    Guid Id,
    string Name,
    string Category,
    bool IsInitial,
    int OrderIndex);

public sealed record WorkflowValidationResult(
    bool IsAllowed,
    string? Reason = null,
    int? CurrentCount = null,
    int? WipLimit = null);

public interface IProjectLookupService
{
    Task<bool> ExistsAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<string?> GetProjectKeyAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectIssueTypeDto>> GetIssueTypesAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectStatusDto>> GetStatusesAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);
}

public interface IWorkflowValidationService
{
    Task<WorkflowValidationResult> CanTransitionAsync(
        Guid projectId,
        Guid fromStatusId,
        Guid toStatusId,
        IReadOnlyCollection<string> userPermissions,
        CancellationToken cancellationToken = default);

    Task<ProjectStatusDto?> GetInitialStatusAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<WorkflowValidationResult> CheckWipLimitAsync(
        Guid boardId,
        Guid statusId,
        CancellationToken cancellationToken = default);
}

public interface IIssueNumberGenerator
{
    Task<int> NextAsync(Guid projectId, CancellationToken cancellationToken = default);
}
