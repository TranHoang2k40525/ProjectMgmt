using System.ComponentModel.DataAnnotations;
using IdentityExperience.Application.Dto;
using IdentityExperience.Application.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.ProjectManagement.Contracts;

namespace ProjectMgmt.Solution.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/projects/{projectId:guid}/members")]
[Route("api/projects/{projectId:guid}/members")]
public class ProjectMembersController : ControllerBase
{
    private static readonly Action<ILogger, string, Exception?> LogEndpointFailure =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(2005, "ProjectMemberEndpointFailure"),
            "Lỗi không xử lý tại endpoint thành viên dự án {Endpoint}.");

    private readonly IRoleServices _roleServices;
    private readonly IPermissionEvaluator _permissions;
    private readonly ICurrentUserContext _currentUser;
    private readonly IProjectLookupService _projects;
    private readonly ILogger<ProjectMembersController> _logger;

    public ProjectMembersController(
        IRoleServices roleServices,
        IPermissionEvaluator permissions,
        ICurrentUserContext currentUser,
        IProjectLookupService projects,
        ILogger<ProjectMembersController> logger)
    {
        _roleServices = roleServices;
        _permissions = permissions;
        _currentUser = currentUser;
        _projects = projects;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetMembers(Guid projectId)
    {
        return await ExecuteWithPermission(
            projectId,
            "member.read",
            "list",
            async (actor, _) => Ok(await _roleServices.GetProjectMembersAsync(projectId)));
    }

    [HttpPost]
    public async Task<IActionResult> AddMember(Guid projectId, [FromBody] RoleDto request)
    {
        return await ExecuteWithPermission(
            projectId,
            "member.invite",
            "add",
            async (actor, _) =>
            {
                var result = await _roleServices.AddProjectMemberAsync(
                    actor,
                    projectId,
                    request.UserId,
                    request.RoleId);
                return result.Success
                    ? StatusCode(StatusCodes.Status201Created, result)
                    : MapWriteFailure(result);
            });
    }

    public sealed class InviteRequest
    {
        [Required, EmailAddress] public string Email { get; set; } = "";
        [Required] public string RoleName { get; set; } = "Developer";
    }

    [HttpPost("invite")]
    public Task<IActionResult> Invite(Guid projectId, InviteRequest request) => ExecuteWithPermission(
        projectId, "member.invite", "invite", async (actor, _) =>
        {
            var result = await _roleServices.InviteProjectMemberByEmailAsync(actor, projectId, request.Email, request.RoleName);
            return result.Success ? StatusCode(201, result) : MapWriteFailure(result);
        });

    [HttpPut("{userId:guid}/role")]
    public async Task<IActionResult> ChangeRole(
        Guid projectId,
        Guid userId,
        [FromBody] RoleDto request)
    {
        return await ExecuteWithPermission(
            projectId,
            "member.role.assign",
            "change-role",
            async (actor, projectScope) =>
            {
                if (projectScope.CreatedByUserId == userId || projectScope.LeadUserId == userId)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new RoleDto
                    {
                        Success = false,
                        ErrorCode = "RBAC_IMMUTABLE_LEAD",
                        Message = "Không thể thay đổi vai trò của Giám đốc dự án hoặc Người tạo dự án."
                    });
                }

                var result = await _roleServices.ChangeProjectMemberRoleAsync(
                    actor,
                    projectId,
                    userId,
                    request.NewRoleId ?? request.RoleId);
                return result.Success ? Ok(result) : MapWriteFailure(result);
            });
    }

    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid projectId, Guid userId)
    {
        return await ExecuteWithPermission(
            projectId,
            "member.remove",
            "remove",
            async (actor, projectScope) =>
            {
                if (projectScope.CreatedByUserId == userId || projectScope.LeadUserId == userId)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new RoleDto
                    {
                        Success = false,
                        ErrorCode = "RBAC_IMMUTABLE_LEAD",
                        Message = "Không thể xóa Giám đốc dự án hoặc Người tạo dự án khỏi dự án."
                    });
                }

                var result = await _roleServices.RemoveProjectMemberAsync(actor, projectId, userId);
                return result.Success ? Ok(result) : MapWriteFailure(result);
            });
    }

    private async Task<IActionResult> ExecuteWithPermission(
        Guid projectId,
        string permissionCode,
        string endpoint,
        Func<Guid, ProjectScopeInfo, Task<IActionResult>> action)
    {
        try
        {
            if (!_currentUser.UserId.HasValue)
            {
                return Unauthorized(new RoleDto
                {
                    Success = false,
                    ErrorCode = "AUTH_UNAUTHENTICATED",
                    Message = "Phiên đăng nhập không hợp lệ."
                });
            }

            var project = await _projects.GetProjectScopeAsync(projectId);
            if (project is null)
            {
                return NotFound(new RoleDto
                {
                    Success = false,
                    ErrorCode = "PROJECT_NOT_FOUND",
                    Message = "Không tìm thấy dự án."
                });
            }

            var allowed = await _permissions.HasPermissionAsync(
                _currentUser.UserId.Value,
                permissionCode,
                "Project",
                projectId,
                project.OrganizationId);
            if (!allowed)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new RoleDto
                    {
                        Success = false,
                        ErrorCode = "RBAC_PERMISSION_DENIED",
                        Message = "Bạn không có quyền thực hiện thao tác này trong dự án."
                    });
            }

            return await action(_currentUser.UserId.Value, project);
        }
        catch (Exception exception)
        {
            LogEndpointFailure(_logger, endpoint, exception);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new RoleDto
                {
                    Success = false,
                    ErrorCode = "RBAC_INTERNAL_ERROR",
                    Message = "Hệ thống thành viên đang gặp lỗi. Vui lòng thử lại sau."
                });
        }
    }

    private ObjectResult MapWriteFailure(RoleDto result)
    {
        return result.ErrorCode switch
        {
            "RBAC_USER_NOT_FOUND" or "RBAC_PROJECT_ROLE_NOT_FOUND" or "RBAC_MEMBER_NOT_FOUND" =>
                NotFound(result),
            "RBAC_MEMBER_ALREADY_EXISTS" or "RBAC_LAST_PROJECT_MANAGER" => Conflict(result),
            "RBAC_SELF_ESCALATION_FORBIDDEN" or "RBAC_SELF_CHANGE_FORBIDDEN" or "RBAC_IMMUTABLE_LEAD" =>
                StatusCode(StatusCodes.Status403Forbidden, result),
            _ => BadRequest(result)
        };
    }
}
