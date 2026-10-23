using IdentityExperience.Application.Dto;
using IdentityExperience.Application.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IdentityExperience.Infrastructure;
using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.ProjectManagement.Contracts;

namespace ProjectMgmt.Solution.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
[Route("api")]
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
    private readonly IdentityExperienceDbContext _db;
    private readonly ILogger<RolesController> _logger;

    public RolesController(
        IRoleServices roleServices,
        IPermissionEvaluator permissions,
        ICurrentUserContext currentUser,
        IProjectLookupService projects,
        IdentityExperienceDbContext db,
        ILogger<RolesController> logger)
    {
        _roleServices = roleServices;
        _permissions = permissions;
        _currentUser = currentUser;
        _projects = projects;
        _db = db;
        _logger = logger;
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles(string? scope)
    {
        try
        {
            var result = await _roleServices.GetRolesAsync(scope);
            if (result.Success && result.Items is not null)
            {
                var grants = await _db.RolePermissions.AsNoTracking().ToListAsync();
                foreach (var role in result.Items)
                    role.PermissionIds = grants.Where(p => p.RoleId == role.RoleId).Select(p => p.PermissionId).ToList();
            }
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

    [HttpGet("admin/users")]
    public async Task<IActionResult> GetAdminUsers()
    {
        if (!await HasSystemPermission("identity.role.manage")) return Forbidden();
        var users = await (from u in _db.Users.AsNoTracking()
                           join p in _db.UserProfiles.AsNoTracking() on u.Id equals p.UserId into profiles
                           from p in profiles.DefaultIfEmpty()
                           orderby u.CreatedAt descending
                           select new { u.Id, u.Email, DisplayName = p == null ? u.Email : p.DisplayName,
                               AvatarUrl = p == null ? null : p.AvatarUrl, JobTitle = p == null ? null : p.JobTitle,
                               u.IsActive, u.IsEmailVerified, u.CreatedAt, u.LastLoginAt }).ToListAsync();
        var assignments = await (from ur in _db.UserRoles.AsNoTracking()
                                 join r in _db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                                 select new { ur.UserId, ur.RoleId, RoleName = r.Name, ur.ScopeType, ur.ScopeId }).ToListAsync();
        return Ok(users.Select(u => new { u.Id, u.Email, u.DisplayName, u.AvatarUrl, u.JobTitle,
            u.IsActive, u.IsEmailVerified, u.CreatedAt, u.LastLoginAt,
            Roles = assignments.Where(r => r.UserId == u.Id).Select(r => new { r.RoleId, r.RoleName, r.ScopeType, r.ScopeId }) }));
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

            var existingRole = await _db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roleId);
            if (existingRole is not null && existingRole.IsSystem)
            {
                return BadRequest(new RoleDto
                {
                    Success = false,
                    ErrorCode = "RBAC_SYSTEM_ROLE_IMMUTABLE",
                    Message = "Không được phép thay đổi quyền của vai trò hệ thống."
                });
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

    [HttpGet("roles/{roleId:guid}/permissions")]
    public async Task<IActionResult> GetRolePermissions(Guid roleId)
    {
        var role = await _db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roleId);
        if (role is null) return NotFound();
        var perms = await (from rp in _db.RolePermissions.AsNoTracking()
                           join p in _db.Permissions.AsNoTracking() on rp.PermissionId equals p.Id
                           where rp.RoleId == roleId
                           select new { p.Id, p.Code, p.Description }).ToListAsync();
        return Ok(new { roleId, permissions = perms });
    }

    [HttpDelete("roles/{roleId:guid}")]
    public async Task<IActionResult> DeleteRole(Guid roleId)
    {
        if (!await HasSystemPermission("identity.role.manage"))
        {
            return Forbidden();
        }

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == roleId);
        if (role is null)
        {
            return NotFound(new RoleDto { Success = false, ErrorCode = "RBAC_ROLE_NOT_FOUND", Message = "Không tìm thấy vai trò." });
        }

        if (role.IsSystem)
        {
            return BadRequest(new RoleDto { Success = false, ErrorCode = "RBAC_SYSTEM_ROLE_IMMUTABLE", Message = "Không được phép xóa vai trò hệ thống." });
        }

        var rolePerms = await _db.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync();
        _db.RolePermissions.RemoveRange(rolePerms);
        _db.Roles.Remove(role);
        await _db.SaveChangesAsync();
        return Ok(new { deleted = true, roleId });
    }

    [HttpGet("permissions/check")]
    public async Task<IActionResult> CheckPermission(
        [FromQuery] string permissionCode,
        [FromQuery] string? scopeType,
        [FromQuery] Guid? scopeId)
    {
        if (!_currentUser.UserId.HasValue) return Unauthenticated();
        var allowed = await _permissions.HasPermissionAsync(
            _currentUser.UserId.Value,
            permissionCode ?? string.Empty,
            string.IsNullOrWhiteSpace(scopeType) ? "System" : scopeType,
            scopeId);
        return Ok(new { allowed, permissionCode });
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
