using System.Security.Claims;
using IdentityExperience.Application.Dto;
using IdentityExperience.Application.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    private readonly ILogger<SkillsController> _logger;

    public SkillsController(
        ISkillServices skillServices,
        ILogger<SkillsController> logger)
    {
        _skillServices = skillServices;
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
            if (!User.IsInRole("Admin"))
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
    public async Task<IActionResult> GetUserSkills(Guid userId)
    {
        try
        {
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
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                return Unauthorized(new SkillDto
                {
                    Success = false,
                    ErrorCode = "AUTH_UNAUTHENTICATED",
                    Message = "Phiên đăng nhập không hợp lệ."
                });
            }

            var result = await _skillServices.UpdateMySkillsAsync(userId, request.Skills);
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
        [FromBody] SkillDto request)
    {
        try
        {
            if (!User.IsInRole("Admin"))
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
