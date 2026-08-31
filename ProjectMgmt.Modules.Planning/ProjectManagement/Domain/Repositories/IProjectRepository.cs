using ProjectMgmt.Modules.Planning.ProjectManagement.Domain.Entities;

namespace ProjectMgmt.Modules.Planning.ProjectManagement.Domain.Repositories;

public interface IProjectRepository
{
    Task<bool> ExistsAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<Project?> GetByIdAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<Project?> GetByKeyAsync(Guid organizationId, string projectKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IssueType>> GetIssueTypesAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowStatus>> GetStatusesAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<WorkflowTransition?> GetTransitionAsync(
        Guid projectId,
        Guid fromStatusId,
        Guid toStatusId,
        CancellationToken cancellationToken = default);

    Task<WorkflowStatus?> GetInitialStatusAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<BoardColumnState?> GetBoardColumnStateAsync(
        Guid boardId,
        Guid statusId,
        CancellationToken cancellationToken = default);

    Task<int> ReserveNextIssueNumberAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task AddAsync(Project project, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record BoardColumnState(Guid ProjectId, int? WipLimit);
