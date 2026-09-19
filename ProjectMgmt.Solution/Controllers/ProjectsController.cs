using Microsoft.AspNetCore.Mvc;
using ProjectMgmt.Modules.Planning.ProjectManagement.Application.IServices;
using ProjectMgmt.ProjectManagement.Contracts;

namespace ProjectMgmt.Solution.Controllers;

[Route("api/projects")]
public class ProjectsController : ApiControllerBase
{
    private readonly IProjectLookupService _projects;
    private readonly IProjectManagementService _management;

    public ProjectsController(
        IProjectLookupService projects,
        IProjectManagementService management)
    {
        _projects = projects;
        _management = management;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProjectDto>>> GetAll(
        [FromQuery] Guid? orgId,
        CancellationToken cancellationToken) =>
        FromResult(await _management.GetProjectsAsync(orgId, cancellationToken));

    [HttpGet("{projectId:guid}")]
    public async Task<ActionResult<ProjectDto>> GetById(
        Guid projectId,
        CancellationToken cancellationToken) =>
        FromResult(await _management.GetProjectByIdAsync(projectId, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ProjectDto>> Create(
        [FromBody] CreateProjectRequestDto request,
        CancellationToken cancellationToken) =>
        FromResult(await _management.CreateProjectAsync(request, cancellationToken));

    [HttpPut("{projectId:guid}")]
    public async Task<ActionResult<ProjectDto>> Update(
        Guid projectId,
        [FromBody] UpdateProjectRequestDto request,
        CancellationToken cancellationToken) =>
        FromResult(await _management.UpdateProjectAsync(projectId, request, cancellationToken));

    [HttpDelete("{projectId:guid}")]
    public async Task<IActionResult> Delete(
        Guid projectId,
        CancellationToken cancellationToken) =>
        FromResult(await _management.DeleteProjectAsync(projectId, cancellationToken));

    [HttpGet("{projectId:guid}/issue-types")]
    public Task<IReadOnlyList<ProjectIssueTypeDto>> GetIssueTypes(
        Guid projectId,
        CancellationToken cancellationToken) =>
        _projects.GetIssueTypesAsync(projectId, cancellationToken);

    [HttpGet("{projectId:guid}/statuses")]
    public Task<IReadOnlyList<ProjectStatusDto>> GetStatuses(
        Guid projectId,
        CancellationToken cancellationToken) =>
        _projects.GetStatusesAsync(projectId, cancellationToken);

    // ==========================================
    // WORKFLOW TRANSITIONS
    // ==========================================

    [HttpGet("{projectId:guid}/workflow/transitions")]
    public async Task<ActionResult<IReadOnlyList<WorkflowTransitionItemDto>>> GetWorkflowTransitions(
        Guid projectId,
        CancellationToken cancellationToken) =>
        FromResult(await _management.GetWorkflowTransitionsAsync(projectId, cancellationToken));

    [HttpPost("{projectId:guid}/workflow/transitions")]
    public async Task<ActionResult<WorkflowTransitionItemDto>> CreateWorkflowTransition(
        Guid projectId,
        [FromBody] CreateWorkflowTransitionRequestDto request,
        CancellationToken cancellationToken) =>
        FromResult(await _management.CreateWorkflowTransitionAsync(projectId, request, cancellationToken));

    // ==========================================
    // BOARDS
    // ==========================================

    [HttpGet("{projectId:guid}/boards")]
    public async Task<ActionResult<IReadOnlyList<BoardDto>>> GetBoards(
        Guid projectId,
        CancellationToken cancellationToken) =>
        FromResult(await _management.GetBoardsAsync(projectId, cancellationToken));

    [HttpPost("{projectId:guid}/boards")]
    public async Task<ActionResult<BoardDto>> CreateBoard(
        Guid projectId,
        [FromBody] CreateBoardRequestDto request,
        CancellationToken cancellationToken) =>
        FromResult(await _management.CreateBoardAsync(projectId, request, cancellationToken));
}
