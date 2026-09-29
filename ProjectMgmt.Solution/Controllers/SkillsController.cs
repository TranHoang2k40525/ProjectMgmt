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
public class SkillsController : ControllerBase
{
    private static readonly Action<ILogger, string, Exception?> LogEndpointFailure =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(2003, "SkillEndpointFailure"),
            "Lỗi không xử lý tại endpoint kỹ năng {Endpoint}.");

    private readonly ISkillServices _skillServices;
    private readonly IPermissionEvaluator _permissions;
    private readonly ICurrentUserContext _currentUser;
    private readonly IProjectLookupService _projects;
    private readonly ILogger<SkillsController> _logger;

    public SkillsController(
        ISkillServices skillServices,
        IPermissionEvaluator permissions,
        ICurrentUserContext currentUser,
        IProjectLookupService projects,
        ILogger<SkillsController> logger)
    {
        _skillServices = skillServices;
        _permissions = permissions;
        _currentUser = currentUser;
        _projects = projects;
        _logger = logger;
    }

    [HttpGet("skills")]
    public async Task<IActionResult> GetCatalog(string? category, string? searchQuery)
    {
        try
        {
            var result = await _skillServices.GetCatalogAsync(category, searchQuery);
            return result.Success ? Ok(result) : BadRequest(result);
        }
        catch (Exception exception)
        {
            return InternalError("skills", exception);
        }
    }

    [HttpPost("skills")]
    public async Task<IActionResult> CreateCatalogSkill([FromBody] SkillDto request)
    {
        try
        {
            if (!await HasSystemPermission("skill.catalog.manage"))
            {
                return Forbidden("IDENTITY_SKILL_CATALOG_FORBIDDEN");
            }

            var result = await _skillServices.CreateCatalogSkillAsync(request);
            if (result.Success)
            {
                return StatusCode(StatusCodes.Status201Created, result);
            }

            return result.ErrorCode == "IDENTITY_SKILL_CODE_EXISTS"
                ? Conflict(result)
                : BadRequest(result);
        }
        catch (Exception exception)
        {
            return InternalError("skills/create", exception);
        }
    }

    [HttpGet("users/{userId:guid}/skills")]
    public async Task<IActionResult> GetUserSkills(Guid userId, Guid? projectId)
    {
        try
        {
            if (!_currentUser.UserId.HasValue)
            {
                return UnauthorizedFailure();
            }

            if (_currentUser.UserId.Value != userId)
            {
                if (!projectId.HasValue
                    || !await HasProjectPermission(projectId.Value, "member.read")
                    || !await _permissions.IsProjectMemberAsync(userId, projectId.Value))
                {
                    return Forbidden("IDENTITY_SKILL_READ_FORBIDDEN");
                }
            }

            var result = await _skillServices.GetUserSkillsAsync(userId);
            return result.Success ? Ok(result) : NotFound(result);
        }
        catch (Exception exception)
        {
            return InternalError("users/skills", exception);
        }
    }

    [HttpPut("users/me/skills")]
    public async Task<IActionResult> UpdateMySkills([FromBody] SkillDto request)
    {
        try
        {
            if (!_currentUser.UserId.HasValue)
            {
                return UnauthorizedFailure();
            }

            var result = await _skillServices.UpdateMySkillsAsync(
                _currentUser.UserId.Value,
                request.Skills);
            if (result.Success)
            {
                return Ok(result);
            }

            return result.ErrorCode == "IDENTITY_USER_NOT_FOUND"
                ? NotFound(result)
                : BadRequest(result);
        }
        catch (Exception exception)
        {
            return InternalError("users/me/skills", exception);
        }
    }

    [HttpPut("users/{userId:guid}/skills/{skillId:guid}/verify")]
    public async Task<IActionResult> VerifyUserSkill(
        Guid userId,
        Guid skillId,
        Guid? projectId,
        [FromBody] SkillDto request)
    {
        try
        {
            var allowed = await HasSystemPermission("skill.verify");
            if (!allowed && projectId.HasValue)
            {
                allowed = await HasProjectPermission(projectId.Value, "skill.verify")
                    && await _permissions.IsProjectMemberAsync(userId, projectId.Value);
            }

            if (!allowed)
            {
                return Forbidden("IDENTITY_SKILL_VERIFY_FORBIDDEN");
            }

            var result = await _skillServices.VerifyUserSkillAsync(
                userId,
                skillId,
                request.Verified,
                request.Level ?? request.ProficiencyLevel);
            return result.Success ? Ok(result) : BadRequest(result);
        }
        catch (Exception exception)
        {
            return InternalError("users/skills/verify", exception);
        }
    }

    private ObjectResult Forbidden(string errorCode)
    {
        return StatusCode(
            StatusCodes.Status403Forbidden,
            new SkillDto
            {
                Success = false,
                ErrorCode = errorCode,
                Message = "Bạn không có quyền thực hiện thao tác này."
            });
    }

    private UnauthorizedObjectResult UnauthorizedFailure()
    {
        return Unauthorized(new SkillDto
        {
            Success = false,
            ErrorCode = "AUTH_UNAUTHENTICATED",
            Message = "Phiên đăng nhập không hợp lệ."
        });
    }

    private Task<bool> HasSystemPermission(string permissionCode)
    {
        return _currentUser.UserId.HasValue
            ? _permissions.HasPermissionAsync(
                _currentUser.UserId.Value,
                permissionCode,
                "System",
                null)
            : Task.FromResult(false);
    }

    private async Task<bool> HasProjectPermission(Guid projectId, string permissionCode)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return false;
        }

        var project = await _projects.GetProjectScopeAsync(projectId);
        return project is not null
            && await _permissions.HasPermissionAsync(
                _currentUser.UserId.Value,
                permissionCode,
                "Project",
                projectId,
                project.OrganizationId);
    }

    private ObjectResult InternalError(string endpoint, Exception exception)
    {
        LogEndpointFailure(_logger, endpoint, exception);
        return StatusCode(
            StatusCodes.Status500InternalServerError,
            new SkillDto
            {
                Success = false,
                ErrorCode = "IDENTITY_INTERNAL_ERROR",
                Message = "Hệ thống kỹ năng đang gặp lỗi. Vui lòng thử lại sau."
            });
    }
}
