using ProjectMgmt.BuildingBlocks.Results;

namespace ProjectMgmt.Modules.Planning.ProjectManagement.Application.IServices;

public interface IProjectManagementService
{
    // Organizations
    Task<Result<IReadOnlyList<OrganizationDto>>> GetOrganizationsAsync();
    Task<Result<OrganizationDto>> CreateOrganizationAsync(CreateOrganizationDto request, Guid ownerUserId);
    Task<Result<OrganizationDto>> CreateOrganizationAsync(CreateOrganizationDto request);

    // Projects
    Task<Result<IReadOnlyList<ProjectDto>>> GetProjectsAsync(Guid? orgId = null);
    Task<Result<ProjectDto>> GetProjectByIdAsync(Guid projectId);
    Task<Result<ProjectDto>> CreateProjectAsync(CreateProjectRequestDto request, Guid creatorUserId);
    Task<Result<ProjectDto>> CreateProjectAsync(CreateProjectRequestDto request);
    Task<Result<ProjectDto>> UpdateProjectAsync(Guid projectId, UpdateProjectRequestDto request);
    Task<Result> DeleteProjectAsync(Guid projectId);

    // Workflow Transitions
    Task<Result<IReadOnlyList<WorkflowTransitionItemDto>>> GetWorkflowTransitionsAsync(Guid projectId);
    Task<Result<WorkflowTransitionItemDto>> CreateWorkflowTransitionAsync(Guid projectId, CreateWorkflowTransitionRequestDto request);
    Task<Result<WorkflowTransitionItemDto>> UpdateWorkflowTransitionAsync(Guid transitionId, UpdateWorkflowTransitionRequestDto request);
    Task<Result> DeleteWorkflowTransitionAsync(Guid transitionId);

    // Boards & Columns
    Task<Result<IReadOnlyList<BoardDto>>> GetBoardsAsync(Guid projectId);
    Task<Result<BoardDto>> CreateBoardAsync(Guid projectId, CreateBoardRequestDto request);
    Task<Result<IReadOnlyList<BoardColumnDto>>> GetBoardColumnsAsync(Guid boardId);
    Task<Result<BoardColumnDto>> CreateBoardColumnAsync(Guid boardId, CreateBoardColumnRequestDto request);
    Task<Result> ReorderBoardColumnsAsync(Guid boardId, ReorderBoardColumnsRequestDto request);
    Task<Result> DeleteBoardColumnAsync(Guid columnId);
    Task<Result<BoardColumnDto>> UpdateBoardColumnAsync(Guid columnId, UpdateBoardColumnRequestDto request);
}
