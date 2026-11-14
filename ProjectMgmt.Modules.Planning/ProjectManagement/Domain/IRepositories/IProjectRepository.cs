using Planning.Domain.Entities;

namespace ProjectMgmt.Modules.Planning.ProjectManagement.Domain.IRepositories;

public interface IProjectRepository
{
    Task<bool> ExistsAsync(Guid projectId);

    Task<Project?> GetByIdAsync(Guid projectId);

    Task<Project?> GetByKeyAsync(Guid organizationId, string projectKey);

    Task<IReadOnlyList<IssueType>> GetIssueTypesAsync(Guid projectId);

    Task<IReadOnlyList<WorkflowStatus>> GetStatusesAsync(Guid projectId);

    Task<WorkflowTransition?> GetTransitionAsync(
        Guid projectId,
        Guid fromStatusId,
        Guid toStatusId);

    Task<WorkflowStatus?> GetInitialStatusAsync(Guid projectId);

    Task<BoardColumnState?> GetBoardColumnStateAsync(
        Guid boardId,
        Guid statusId);

    Task<int> ReserveNextIssueNumberAsync(Guid projectId);

    Task<IReadOnlyList<Organization>> GetOrganizationsAsync();

    Task<Organization?> GetOrganizationByIdAsync(Guid orgId);

    Task AddOrganizationAsync(Organization organization);

    Task<IReadOnlyList<Project>> GetAllProjectsAsync(Guid? orgId = null);

    void UpdateProject(Project project);

    Task AddIssueTypesAsync(IEnumerable<IssueType> issueTypes);

    Task AddWorkflowStatusesAsync(IEnumerable<WorkflowStatus> statuses);

    Task AddWorkflowTransitionsAsync(IEnumerable<WorkflowTransition> transitions);

    Task AddBoardAsync(Board board);

    Task AddBoardColumnsAsync(IEnumerable<BoardColumn> columns);

    Task<IReadOnlyList<WorkflowTransition>> GetTransitionsByProjectAsync(Guid projectId);

    Task<WorkflowTransition?> GetTransitionByIdAsync(Guid transitionId);

    Task AddWorkflowTransitionAsync(WorkflowTransition transition);

    void DeleteWorkflowTransition(WorkflowTransition transition);

    Task<IReadOnlyList<Board>> GetBoardsByProjectAsync(Guid projectId);

    Task<Board?> GetBoardByIdAsync(Guid boardId);

    Task<IReadOnlyList<BoardColumn>> GetBoardColumnsAsync(Guid boardId);

    Task<BoardColumn?> GetBoardColumnByIdAsync(Guid columnId);

    Task AddBoardColumnAsync(BoardColumn column);

    void DeleteBoardColumn(BoardColumn column);

    Task AddAsync(Project project);

    Task SaveChangesAsync();
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
