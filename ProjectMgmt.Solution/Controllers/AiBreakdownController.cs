using IdentityExperience.Application.Dto;
using IdentityExperience.Application.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectMgmt.IdentityAccess.Contracts;

namespace ProjectMgmt.Solution.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/ai/breakdown")]
public class AiBreakdownController : ControllerBase
{
    private static readonly Action<ILogger, string, Exception?> LogEndpointFailure =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(2021, "AiBreakdownEndpointFailure"),
            "Lỗi không xử lý tại endpoint AI breakdown {Endpoint}.");

    private readonly IAiBreakdownServices _aiServices;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILogger<AiBreakdownController> _logger;

    public AiBreakdownController(
        IAiBreakdownServices aiServices,
        ICurrentUserContext currentUser,
        ILogger<AiBreakdownController> logger)
    {
        _aiServices = aiServices;
        _currentUser = currentUser;
        _logger = logger;
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] AiBreakdownDto request)
    {
        return await ExecuteAsync(
            "generate",
            actor => _aiServices.GenerateAsync(actor, request),
            StatusCodes.Status201Created);
    }

    [HttpGet("{generationId:guid}")]
    public async Task<IActionResult> Get(Guid generationId)
    {
        return await ExecuteAsync(
            "get",
            actor => _aiServices.GetAsync(actor, generationId));
    }

    [HttpPost("{generationId:guid}/feedback")]
    public async Task<IActionResult> SaveFeedback(
        Guid generationId,
        [FromBody] AiBreakdownDto request)
    {
        return await ExecuteAsync(
            "feedback",
            actor => _aiServices.SaveFeedbackAsync(actor, generationId, request));
    }

    [HttpPost("{generationId:guid}/apply")]
    public async Task<IActionResult> Apply(
        Guid generationId,
        [FromBody] AiBreakdownDto request)
    {
        return await ExecuteAsync(
            "apply",
            actor => _aiServices.ApplyAsync(actor, generationId, request));
    }

    private async Task<IActionResult> ExecuteAsync(
        string endpoint,
        Func<Guid, Task<AiBreakdownDto>> operation,
        int successStatusCode = StatusCodes.Status200OK)
    {
        try
        {
            if (!_currentUser.UserId.HasValue)
            {
                return Unauthorized(Failure(
                    "AUTH_UNAUTHENTICATED",
                    "Phiên đăng nhập không hợp lệ."));
            }

            var result = await operation(_currentUser.UserId.Value);
            if (result.Success)
            {
                return StatusCode(successStatusCode, result);
            }

            return MapFailure(result);
        }
        catch (Exception exception)
        {
            LogEndpointFailure(_logger, endpoint, exception);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                Failure(
                    "AI_BREAKDOWN_INTERNAL_ERROR",
                    "Hệ thống AI breakdown đang gặp lỗi. Vui lòng thử lại sau."));
        }
    }

    private ObjectResult MapFailure(AiBreakdownDto result)
    {
        if (result.ErrorCode?.Contains("FORBIDDEN", StringComparison.Ordinal) == true)
        {
            return StatusCode(StatusCodes.Status403Forbidden, result);
        }

        if (result.ErrorCode is "AI_BREAKDOWN_NOT_FOUND"
            or "AI_BREAKDOWN_ISSUE_NOT_FOUND"
            or "AI_SUGGESTION_NOT_FOUND")
        {
            return NotFound(result);
        }

        if (result.ErrorCode is "AI_BREAKDOWN_ALREADY_APPLIED"
            or "AI_BREAKDOWN_NOT_COMPLETED"
            or "AI_BREAKDOWN_SUGGESTION_UNAVAILABLE")
        {
            return Conflict(result);
        }

        return BadRequest(result);
    }

    private static AiBreakdownDto Failure(string errorCode, string message)
    {
        return new AiBreakdownDto
        {
            Success = false,
            ErrorCode = errorCode,
            Message = message
        };
    }
}
