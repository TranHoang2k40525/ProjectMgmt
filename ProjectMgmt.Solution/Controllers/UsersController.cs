using System.Security.Claims;
using IdentityExperience.Application.Dto;
using IdentityExperience.Application.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectMgmt.IdentityAccess.Contracts;

namespace ProjectMgmt.Solution.Controllers;

[Route("api/users")]
public class UsersController : ApiControllerBase
{
    private static readonly Action<ILogger, Exception?> LogChangePasswordFailure =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(2002, "ChangePasswordEndpointFailure"),
            "Lỗi không xử lý tại endpoint đổi mật khẩu.");

    private readonly IUserLookupService _users;
    private readonly IUserSkillService _skills;
    private readonly IAccountServices _accountServices;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserLookupService users,
        IUserSkillService skills,
        IAccountServices accountServices,
        ILogger<UsersController> logger)
    {
        _users = users;
        _skills = skills;
        _accountServices = accountServices;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserDisplayInfo>>> GetAllUsers(CancellationToken cancellationToken)
    {
        var sampleIds = new[]
        {
            Guid.Parse("11111111-0000-0000-0000-000000000001"),
            Guid.Parse("11111111-0000-0000-0000-000000000002"),
            Guid.Parse("11111111-0000-0000-0000-000000000003")
        };
        var users = await _users.GetDisplayInfoAsync(sampleIds, cancellationToken);
        if (users.Count == 0)
        {
            users = new List<UserDisplayInfo>
            {
                new UserDisplayInfo(sampleIds[0], "Hoàng Admin (System Lead)", null),
                new UserDisplayInfo(sampleIds[1], "Nguyễn Văn Dev (Technical Lead)", null),
                new UserDisplayInfo(sampleIds[2], "Trần Thị Scrum (Scrum Master)", null)
            };
        }
        return Ok(users);
    }

    [HttpGet("{userId:guid}/display")]
    public async Task<ActionResult<UserDisplayInfo>> GetDisplay(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await _users.GetDisplayInfoAsync(userId, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost("skills/query")]
    public Task<IReadOnlyList<UserSkillInfo>> GetSkills(
        [FromBody] IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken) =>
        _skills.GetUserSkillsAsync(userIds, cancellationToken);

    [Authorize]
    [HttpPut("/api/v1/users/me/password")]
    public async Task<IActionResult> ChangePassword([FromBody] AccountDto request)
    {
        try
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdValue, out var userId))
            {
                return Unauthorized(new Result
                {
                    Success = false,
                    ErrorCode = "AUTH_UNAUTHENTICATED",
                    Message = "Phiên đăng nhập không hợp lệ."
                });
            }

            var result = await _accountServices.ChangePasswordAsync(
                userId,
                request.CurrentPassword ?? request.Password,
                request.NewPassword);
            if (result.Success)
            {
                return Ok(result);
            }

            return result.ErrorCode switch
            {
                "AUTH_UNAUTHENTICATED" => Unauthorized(result),
                "AUTH_ACCOUNT_DISABLED" or "AUTH_EMAIL_NOT_VERIFIED" =>
                    StatusCode(StatusCodes.Status403Forbidden, result),
                "AUTH_ACCOUNT_NOT_FOUND" => NotFound(result),
                "AUTH_PASSWORD_CONFLICT" => Conflict(result),
                _ => BadRequest(result)
            };
        }
        catch (Exception exception)
        {
            LogChangePasswordFailure(_logger, exception);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new Result
                {
                    Success = false,
                    ErrorCode = "AUTH_INTERNAL_ERROR",
                    Message = "Hệ thống xác thực đang gặp lỗi. Vui lòng thử lại sau."
                });
        }
    }
}
