namespace ProjectMgmt.ProjectManagement.Contracts;

public class ProjectIssueTypeDto
{
    public ProjectIssueTypeDto(Guid id, string name, bool isSubtask, int hierarchyLevel)
    {
        Id = id;
        Name = name;
        IsSubtask = isSubtask;
        HierarchyLevel = hierarchyLevel;
    }

    public Guid Id { get; set; }
    public string Name { get; set; }
    public bool IsSubtask { get; set; }
    public int HierarchyLevel { get; set; }
}

public class ProjectStatusDto
{
    public ProjectStatusDto(Guid id, string name, string category, bool isInitial, int orderIndex)
    {
        Id = id;
        Name = name;
        Category = category;
        IsInitial = isInitial;
        OrderIndex = orderIndex;
    }

    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Category { get; set; }
    public bool IsInitial { get; set; }
    public int OrderIndex { get; set; }
}

public class WorkflowValidationResult
{
    public WorkflowValidationResult(
        bool isAllowed,
        string? reason = null,
        int? currentCount = null,
        int? wipLimit = null)
    {
        IsAllowed = isAllowed;
        Reason = reason;
        CurrentCount = currentCount;
        WipLimit = wipLimit;
    }

    public bool IsAllowed { get; set; }
    public string? Reason { get; set; }
    public int? CurrentCount { get; set; }
    public int? WipLimit { get; set; }
}

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

    Task<ProjectStatusDto?> GetInitialStatusAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<WorkflowValidationResult> CheckWipLimitAsync(
        Guid boardId,
        Guid statusId,
        CancellationToken cancellationToken = default);
}

public interface IIssueNumberGenerator
{
    Task<int> NextAsync(Guid projectId, CancellationToken cancellationToken = default);
}

public class SprintLookupInfo
{
    public SprintLookupInfo(Guid id, Guid projectId, string status)
    {
        Id = id;
        ProjectId = projectId;
        Status = status;
    }

    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Status { get; set; }
}

public interface ISprintLookupService
{
    Task<SprintLookupInfo?> GetByIdAsync(Guid sprintId, CancellationToken cancellationToken = default);
}
