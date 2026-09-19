using Planning.Domain.Entities;

namespace ProjectMgmt.Modules.Planning.ProjectManagement.Domain.IRepositories;

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

    Task<IReadOnlyList<Organization>> GetOrganizationsAsync(CancellationToken cancellationToken = default);

    Task<Organization?> GetOrganizationByIdAsync(Guid orgId, CancellationToken cancellationToken = default);

    Task AddOrganizationAsync(Organization organization, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Project>> GetAllProjectsAsync(Guid? orgId = null, CancellationToken cancellationToken = default);

    void UpdateProject(Project project);

    Task AddIssueTypesAsync(IEnumerable<IssueType> issueTypes, CancellationToken cancellationToken = default);

    Task AddWorkflowStatusesAsync(IEnumerable<WorkflowStatus> statuses, CancellationToken cancellationToken = default);

    Task AddWorkflowTransitionsAsync(IEnumerable<WorkflowTransition> transitions, CancellationToken cancellationToken = default);

    Task AddBoardAsync(Board board, CancellationToken cancellationToken = default);

    Task AddBoardColumnsAsync(IEnumerable<BoardColumn> columns, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowTransition>> GetTransitionsByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<WorkflowTransition?> GetTransitionByIdAsync(Guid transitionId, CancellationToken cancellationToken = default);

    Task AddWorkflowTransitionAsync(WorkflowTransition transition, CancellationToken cancellationToken = default);

    void DeleteWorkflowTransition(WorkflowTransition transition);

    Task<IReadOnlyList<Board>> GetBoardsByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<Board?> GetBoardByIdAsync(Guid boardId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BoardColumn>> GetBoardColumnsAsync(Guid boardId, CancellationToken cancellationToken = default);

    Task<BoardColumn?> GetBoardColumnByIdAsync(Guid columnId, CancellationToken cancellationToken = default);

    Task AddBoardColumnAsync(BoardColumn column, CancellationToken cancellationToken = default);

    void DeleteBoardColumn(BoardColumn column);

    Task AddAsync(Project project, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public class BoardColumnState
{
    public BoardColumnState(Guid projectId, int? wipLimit)
    {
        ProjectId = projectId;
        WipLimit = wipLimit;
    }

    public Guid ProjectId { get; }
    public int? WipLimit { get; }
}
