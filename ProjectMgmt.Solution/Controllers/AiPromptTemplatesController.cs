using IdentityExperience.Application.Dto;
using IdentityExperience.Application.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectMgmt.IdentityAccess.Contracts;

namespace ProjectMgmt.Solution.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/ai/prompt-templates")]
public class AiPromptTemplatesController : ControllerBase
{
    private static readonly Action<ILogger, string, Exception?> LogEndpointFailure =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(2022, "AiPromptEndpointFailure"),
            "Lỗi không xử lý tại endpoint AI prompt {Endpoint}.");

    private readonly IAiBreakdownServices _aiServices;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILogger<AiPromptTemplatesController> _logger;

    public AiPromptTemplatesController(
        IAiBreakdownServices aiServices,
        ICurrentUserContext currentUser,
        ILogger<AiPromptTemplatesController> logger)
    {
        _aiServices = aiServices;
        _currentUser = currentUser;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get(string? taskType)
    {
        try
        {
            var result = await _aiServices.GetPromptTemplatesAsync(taskType);
            return result.Success ? Ok(result) : BadRequest(result);
        }
        catch (Exception exception)
        {
            return InternalError("list", exception);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AiPromptTemplateDto request)
    {
        try
        {
            if (!_currentUser.UserId.HasValue)
            {
                return UnauthorizedFailure();
            }

            var result = await _aiServices.CreatePromptTemplateAsync(
                _currentUser.UserId.Value,
                request);
            if (result.Success)
            {
                return StatusCode(StatusCodes.Status201Created, result);
            }

            return result.ErrorCode == "AI_PROMPT_FORBIDDEN"
                ? StatusCode(StatusCodes.Status403Forbidden, result)
                : result.ErrorCode == "AI_PROMPT_VERSION_CONFLICT"
                    ? Conflict(result)
                    : BadRequest(result);
        }
        catch (Exception exception)
        {
            return InternalError("create", exception);
        }
    }

    [HttpPut("{promptId:guid}/activate")]
    public async Task<IActionResult> Activate(Guid promptId)
    {
        try
        {
            if (!_currentUser.UserId.HasValue)
            {
                return UnauthorizedFailure();
            }

            var result = await _aiServices.ActivatePromptTemplateAsync(
                _currentUser.UserId.Value,
                promptId);
            if (result.Success)
            {
                return Ok(result);
            }

            return result.ErrorCode == "AI_PROMPT_FORBIDDEN"
                ? StatusCode(StatusCodes.Status403Forbidden, result)
                : NotFound(result);
        }
        catch (Exception exception)
        {
            return InternalError("activate", exception);
        }
    }

    private UnauthorizedObjectResult UnauthorizedFailure()
    {
        return Unauthorized(new AiPromptTemplateDto
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
            new AiPromptTemplateDto
            {
                Success = false,
                ErrorCode = "AI_PROMPT_INTERNAL_ERROR",
                Message = "Hệ thống quản lý prompt đang gặp lỗi. Vui lòng thử lại sau."
            });
    }
}
