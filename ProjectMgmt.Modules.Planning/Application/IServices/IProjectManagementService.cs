using ProjectMgmt.BuildingBlocks.Results;

namespace ProjectMgmt.Modules.Planning.ProjectManagement.Application.IServices;

public interface IProjectManagementService
{
    // Organizations
    Task<Result<IReadOnlyList<OrganizationDto>>> GetOrganizationsAsync(CancellationToken cancellationToken = default);
    Task<Result<OrganizationDto>> CreateOrganizationAsync(CreateOrganizationDto request, CancellationToken cancellationToken = default);

    // Projects
    Task<Result<IReadOnlyList<ProjectDto>>> GetProjectsAsync(Guid? orgId = null, CancellationToken cancellationToken = default);
    Task<Result<ProjectDto>> GetProjectByIdAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Result<ProjectDto>> CreateProjectAsync(CreateProjectRequestDto request, CancellationToken cancellationToken = default);
    Task<Result<ProjectDto>> UpdateProjectAsync(Guid projectId, UpdateProjectRequestDto request, CancellationToken cancellationToken = default);
    Task<Result> DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    // Workflow Transitions
    Task<Result<IReadOnlyList<WorkflowTransitionItemDto>>> GetWorkflowTransitionsAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Result<WorkflowTransitionItemDto>> CreateWorkflowTransitionAsync(Guid projectId, CreateWorkflowTransitionRequestDto request, CancellationToken cancellationToken = default);
    Task<Result<WorkflowTransitionItemDto>> UpdateWorkflowTransitionAsync(Guid transitionId, UpdateWorkflowTransitionRequestDto request, CancellationToken cancellationToken = default);
    Task<Result> DeleteWorkflowTransitionAsync(Guid transitionId, CancellationToken cancellationToken = default);

    // Boards & Columns
    Task<Result<IReadOnlyList<BoardDto>>> GetBoardsAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Result<BoardDto>> CreateBoardAsync(Guid projectId, CreateBoardRequestDto request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<BoardColumnDto>>> GetBoardColumnsAsync(Guid boardId, CancellationToken cancellationToken = default);
    Task<Result<BoardColumnDto>> CreateBoardColumnAsync(Guid boardId, CreateBoardColumnRequestDto request, CancellationToken cancellationToken = default);
    Task<Result> ReorderBoardColumnsAsync(Guid boardId, ReorderBoardColumnsRequestDto request, CancellationToken cancellationToken = default);
    Task<Result> DeleteBoardColumnAsync(Guid columnId, CancellationToken cancellationToken = default);
}
