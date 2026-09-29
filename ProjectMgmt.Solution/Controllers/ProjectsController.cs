using Microsoft.AspNetCore.Mvc;
using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.Modules.Planning.ProjectManagement.Application.IServices;
using ProjectMgmt.ProjectManagement.Contracts;
using ProjectMgmt.Solution.Services;

namespace ProjectMgmt.Solution.Controllers;

[Route("api/v1/projects")]
public class ProjectsController : ApiControllerBase
{
    private static readonly Action<ILogger, Exception?> LogCreateFailure =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(2102, "ProjectCreateFailure"),
            "Lỗi không xử lý khi tạo dự án.");

    private readonly IProjectLookupService _projects;
    private readonly IProjectManagementService _management;
    private readonly IWorkspaceProvisioningService _provisioning;
    private readonly IPermissionEvaluator _permissions;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(
        IProjectLookupService projects,
        IProjectManagementService management,
        IWorkspaceProvisioningService provisioning,
        IPermissionEvaluator permissions,
        ICurrentUserContext currentUser,
        ILogger<ProjectsController> logger)
    {
        _projects = projects;
        _management = management;
        _provisioning = provisioning;
        _permissions = permissions;
        _currentUser = currentUser;
        _logger = logger;
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
        [FromBody] CreateProjectRequestDto request)
    {
        try
        {
            if (!_currentUser.UserId.HasValue)
            {
                return Unauthorized(new
                {
                    success = false,
                    errorCode = "AUTH_UNAUTHENTICATED",
                    message = "Phiên đăng nhập không hợp lệ."
                });
            }

            var allowed = request.OrgId != Guid.Empty
                && await _permissions.HasPermissionAsync(
                    _currentUser.UserId.Value,
                    "project.create",
                    "Organization",
                    request.OrgId);
            if (!allowed)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        success = false,
                        errorCode = "PROJECT_CREATE_FORBIDDEN",
                        message = "Bạn không có quyền tạo dự án trong tổ chức này."
                    });
            }

            var result = await _provisioning.CreateProjectAsync(
                request,
                _currentUser.UserId.Value);
            return result.IsSuccess
                ? StatusCode(StatusCodes.Status201Created, result.Value)
                : FromResult(result);
        }
        catch (Exception exception)
        {
            LogCreateFailure(_logger, exception);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    success = false,
                    errorCode = "PROJECT_CREATE_FAILED",
                    message = "Không thể tạo dự án. Vui lòng thử lại sau."
                });
        }
    }

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
