namespace ProjectMgmt.ProjectManagement.Contracts;

public record ProjectIssueTypeDto(Guid Id, Guid ProjectId, string Name, string ColorHex, int HierarchyLevel, bool IsSubtask, int OrderIndex);

public record ProjectStatusDto(Guid Id, Guid ProjectId, string Name, string Category, string ColorHex, int OrderIndex, bool IsInitial);

public class ProjectScopeInfo
{
    public Guid ProjectId { get; set; }
    public Guid OrganizationId { get; set; }
    public bool IsArchived { get; set; }
}

public interface IProjectLookupService
{
    Task<IReadOnlyList<ProjectIssueTypeDto>> GetIssueTypesAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectStatusDto>> GetStatusesAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<ProjectScopeInfo?> GetProjectScopeAsync(Guid projectId);
}
