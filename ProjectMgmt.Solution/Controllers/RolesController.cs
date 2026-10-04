using IdentityExperience.Application.Dto;
using IdentityExperience.Application.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.ProjectManagement.Contracts;

namespace ProjectMgmt.Solution.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
public class RolesController : ControllerBase
{
    private static readonly Action<ILogger, string, Exception?> LogEndpointFailure =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(2004, "RoleEndpointFailure"),
            "Lỗi không xử lý tại endpoint RBAC {Endpoint}.");

    private readonly IRoleServices _roleServices;
    private readonly IPermissionEvaluator _permissions;
    private readonly ICurrentUserContext _currentUser;
    private readonly IProjectLookupService _projects;
    private readonly ILogger<RolesController> _logger;

    public RolesController(
        IRoleServices roleServices,
        IPermissionEvaluator permissions,
        ICurrentUserContext currentUser,
        IProjectLookupService projects,
        ILogger<RolesController> logger)
    {
        _roleServices = roleServices;
        _permissions = permissions;
        _currentUser = currentUser;
        _projects = projects;
        _logger = logger;
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles(string? scope)
    {
        try
        {
            var result = await _roleServices.GetRolesAsync(scope);
            return result.Success ? Ok(result) : BadRequest(result);
        }
        catch (Exception exception)
        {
            return InternalError("roles", exception);
        }
    }

    [HttpGet("permissions")]
    public async Task<IActionResult> GetPermissions()
    {
        try
        {
            return Ok(await _roleServices.GetPermissionsAsync());
        }
        catch (Exception exception)
        {
            return InternalError("permissions", exception);
        }
    }

    [HttpPost("roles")]
    public async Task<IActionResult> CreateRole([FromBody] RoleDto request)
    {
        try
        {
            if (!await HasSystemPermission("identity.role.manage"))
            {
                return Forbidden();
            }

            var result = await _roleServices.CreateRoleAsync(request);
            if (result.Success)
            {
                return StatusCode(StatusCodes.Status201Created, result);
            }

            return result.ErrorCode == "RBAC_ROLE_NAME_EXISTS"
                ? Conflict(result)
                : BadRequest(result);
        }
        catch (Exception exception)
        {
            return InternalError("roles/create", exception);
        }
    }

    [HttpPut("roles/{roleId:guid}/permissions")]
    public async Task<IActionResult> UpdateRolePermissions(
        Guid roleId,
        [FromBody] RoleDto request)
    {
        try
        {
            if (!await HasSystemPermission("identity.role.manage"))
            {
                return Forbidden();
            }

            var result = await _roleServices.UpdateRolePermissionsAsync(roleId, request.PermissionIds);
            return result.Success
                ? Ok(result)
                : result.ErrorCode == "RBAC_ROLE_NOT_FOUND"
                    ? NotFound(result)
                    : BadRequest(result);
        }
        catch (Exception exception)
        {
            return InternalError("roles/permissions", exception);
        }
    }

    [HttpGet("users/{userId:guid}/roles")]
    public async Task<IActionResult> GetUserRoles(
        Guid userId,
        string? scopeType,
        Guid? scopeId)
    {
        try
        {
            if (!_currentUser.UserId.HasValue)
            {
                return Unauthenticated();
            }

            if (_currentUser.UserId.Value != userId
                && !await CanReadScope(scopeType, scopeId))
            {
                return Forbidden();
            }

            var result = await _roleServices.GetUserRolesAsync(userId, scopeType, scopeId);
            return result.Success ? Ok(result) : BadRequest(result);
        }
        catch (Exception exception)
        {
            return InternalError("users/roles", exception);
        }
    }

    [HttpPost("users/{userId:guid}/roles")]
    public async Task<IActionResult> AssignUserRole(Guid userId, [FromBody] RoleDto request)
    {
        try
        {
            if (!_currentUser.UserId.HasValue)
            {
                return Unauthenticated();
            }

            if (!await CanManageScope(request.ScopeType, request.ScopeId, "member.role.assign"))
            {
                return Forbidden();
            }

            var result = await _roleServices.AssignUserRoleAsync(
                _currentUser.UserId.Value,
                userId,
                request);
            if (result.Success)
            {
                return StatusCode(StatusCodes.Status201Created, result);
            }

            return MapWriteFailure(result);
        }
        catch (Exception exception)
        {
            return InternalError("users/roles/assign", exception);
        }
    }

    [HttpDelete("users/{userId:guid}/roles/{userRoleId:guid}")]
    public async Task<IActionResult> RemoveUserRole(Guid userId, Guid userRoleId)
    {
        try
        {
            if (!_currentUser.UserId.HasValue)
            {
                return Unauthenticated();
            }

            var assignments = await _roleServices.GetUserRolesAsync(userId, null, null);
            var assignment = assignments.Roles?.FirstOrDefault(role => role.UserRoleId == userRoleId);
            if (assignment is null)
            {
                return NotFound(new RoleDto
                {
                    Success = false,
                    ErrorCode = "RBAC_USER_ROLE_NOT_FOUND",
                    Message = "Không tìm thấy vai trò đã gán."
                });
            }

            if (!await CanManageScope(
                    assignment.ScopeType,
                    assignment.ScopeId,
                    "member.role.assign"))
            {
                return Forbidden();
            }

            var result = await _roleServices.RemoveUserRoleAsync(
                _currentUser.UserId.Value,
                userId,
                userRoleId);
            return result.Success ? Ok(result) : MapWriteFailure(result);
        }
        catch (Exception exception)
        {
            return InternalError("users/roles/remove", exception);
        }
    }

    private async Task<bool> HasSystemPermission(string permissionCode)
    {
        return _currentUser.UserId.HasValue
            && await _permissions.HasPermissionAsync(
                _currentUser.UserId.Value,
                permissionCode,
                "System",
                null);
    }

    private async Task<bool> CanReadScope(string? scopeType, Guid? scopeId)
    {
        return await CanManageScope(scopeType, scopeId, "member.read");
    }

    private async Task<bool> CanManageScope(
        string? scopeType,
        Guid? scopeId,
        string projectPermission)
    {
        if (!_currentUser.UserId.HasValue || !scopeId.HasValue)
        {
            return false;
        }

        if (scopeType == "Project")
        {
            var project = await _projects.GetProjectScopeAsync(scopeId.Value);
            return project is not null
                && await _permissions.HasPermissionAsync(
                    _currentUser.UserId.Value,
                    projectPermission,
                    "Project",
                    project.ProjectId,
                    project.OrganizationId);
        }

        if (scopeType == "Organization")
        {
            return await _permissions.HasPermissionAsync(
                _currentUser.UserId.Value,
                "identity.role.manage",
                "Organization",
                scopeId.Value);
        }

        return false;
    }

    private ObjectResult MapWriteFailure(RoleDto result)
    {
        return result.ErrorCode switch
        {
            "RBAC_USER_NOT_FOUND" or "RBAC_ROLE_NOT_FOUND" or "RBAC_USER_ROLE_NOT_FOUND" =>
                NotFound(result),
            "RBAC_ROLE_ALREADY_ASSIGNED" or "RBAC_LAST_PROJECT_MANAGER" => Conflict(result),
            "RBAC_SELF_ESCALATION_FORBIDDEN" or "RBAC_SELF_CHANGE_FORBIDDEN" =>
                StatusCode(StatusCodes.Status403Forbidden, result),
            _ => BadRequest(result)
        };
    }

    private ObjectResult Forbidden()
    {
        return StatusCode(
            StatusCodes.Status403Forbidden,
            new RoleDto
            {
                Success = false,
                ErrorCode = "RBAC_PERMISSION_DENIED",
                Message = "Bạn không có quyền thực hiện thao tác này."
            });
    }

    private UnauthorizedObjectResult Unauthenticated()
    {
        return Unauthorized(new RoleDto
        {
            Success = false,
            ErrorCode = "AUTH_UNAUTHENTICATED",
            Message = "Phiên đăng nhập không hợp lệ."
        });
    }

    private ObjectResult InternalError(string endpoint, Exception exception)
    {
        LogEndpointFailure(_logger, endpoint, exception);
        return StatusCode(
            StatusCodes.Status500InternalServerError,
            new RoleDto
            {
                Success = false,
                ErrorCode = "RBAC_INTERNAL_ERROR",
                Message = "Hệ thống phân quyền đang gặp lỗi. Vui lòng thử lại sau."
            });
    }
}
