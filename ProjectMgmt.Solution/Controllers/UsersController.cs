using IdentityExperience.Application.Dto;
using IdentityExperience.Application.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectMgmt.IdentityAccess.Contracts;

namespace ProjectMgmt.Solution.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
[Route("api/v1/users")]
public class UsersController : ControllerBase
{
    private static readonly Action<ILogger, string, Exception?> LogEndpointFailure =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(2002, "UserEndpointFailure"),
            "Lỗi không xử lý tại endpoint người dùng {Endpoint}.");

    private readonly IProfileServices _profileServices;
    private readonly IAccountServices _accountServices;
    private readonly IForYouServices _forYouServices;
    private readonly IUserLookupService _userLookup;
    private readonly IUserSkillService _userSkills;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IProfileServices profileServices,
        IAccountServices accountServices,
        IForYouServices forYouServices,
        IUserLookupService userLookup,
        IUserSkillService userSkills,
        ICurrentUserContext currentUser,
        ILogger<UsersController> logger)
    {
        _profileServices = profileServices;
        _accountServices = accountServices;
        _forYouServices = forYouServices;
        _userLookup = userLookup;
        _userSkills = userSkills;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>
    /// Danh sách người dùng đang hoạt động (dùng cho chọn Lead/thành viên dự án).
    /// Hỗ trợ cả /api/users (cũ) và /api/v1/users (mới).
    /// </summary>
    [HttpGet]
    [HttpGet("~/api/users")]
    public async Task<IActionResult> GetAllUsers(CancellationToken cancellationToken)
    {
        try
        {
            var identity = HttpContext.RequestServices.GetRequiredService<IdentityExperience.Infrastructure.IdentityExperienceDbContext>();
            return Ok(await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                from user in identity.Users
                join profile in identity.UserProfiles on user.Id equals profile.UserId
                where user.IsActive && user.IsEmailVerified
                orderby profile.DisplayName
                select new { userId = user.Id, profile.DisplayName, user.Email, profile.AvatarUrl }, cancellationToken));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return InternalError("users", exception);
        }
    }

    [HttpGet("{userId:guid}/display")]
    [HttpGet("~/api/users/{userId:guid}/display")]
    public async Task<IActionResult> GetDisplay(
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userLookup.GetDisplayInfoAsync(userId, cancellationToken);
            return user is null ? NotFound() : Ok(user);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return InternalError("users/display", exception);
        }
    }

    [HttpPost("skills/query")]
    [HttpPost("~/api/users/skills/query")]
    public async Task<IActionResult> GetSkills(
        [FromBody] IReadOnlyCollection<Guid> userIds)
    {
        try
        {
            return Ok(await _userSkills.GetUserSkillsAsync(userIds));
        }
        catch (Exception exception)
        {
            return InternalError("users/skills/query", exception);
        }
    }

    [HttpGet("for-you")]
    public async Task<IActionResult> GetForYou()
    {
        try
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthenticated();
            }

            return Ok(await _forYouServices.GetAsync(userId));
        }
        catch (Exception exception)
        {
            return InternalError("for-you", exception);
        }
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile()
    {
        try
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthenticated();
            }

            var result = await _profileServices.GetMyProfileAsync(userId);
            return result.Success ? Ok(result) : NotFound(result);
        }
        catch (Exception exception)
        {
            return InternalError("me", exception);
        }
    }

    [HttpPut("me/profile")]
    public async Task<IActionResult> UpdateMyProfile([FromBody] ProfileDto request)
    {
        try
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthenticated();
            }

            var result = await _profileServices.UpdateMyProfileAsync(userId, request);
            if (result.Success)
            {
                return Ok(result);
            }

            return result.ErrorCode switch
            {
                "IDENTITY_PROFILE_NOT_FOUND" => NotFound(result),
                "IDENTITY_PHONE_ALREADY_EXISTS" => Conflict(result),
                _ => BadRequest(result)
            };
        }
        catch (Exception exception)
        {
            return InternalError("me/profile", exception);
        }
    }

    [HttpPost("me/avatar")]
    public async Task<IActionResult> UpdateAvatar(IFormFile? avatar)
    {
        try
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthenticated();
            }

            if (avatar is null)
            {
                return BadRequest(new AvatarResult
                {
                    Success = false,
                    ErrorCode = "IDENTITY_AVATAR_REQUIRED",
                    Message = "Phải chọn tệp ảnh đại diện."
                });
            }

            await using var content = avatar.OpenReadStream();
            var result = await _profileServices.UpdateAvatarAsync(
                userId,
                content,
                avatar.Length,
                avatar.ContentType);
            if (result.Success)
            {
                return Ok(result);
            }

            return result.ErrorCode switch
            {
                "IDENTITY_PROFILE_NOT_FOUND" => NotFound(result),
                "IDENTITY_AVATAR_SIZE_INVALID" =>
                    StatusCode(StatusCodes.Status413PayloadTooLarge, result),
                "IDENTITY_AVATAR_TYPE_INVALID" =>
                    StatusCode(StatusCodes.Status415UnsupportedMediaType, result),
                _ => BadRequest(result)
            };
        }
        catch (Exception exception)
        {
            return InternalError("me/avatar", exception);
        }
    }

    [HttpPut("me/password")]
    public async Task<IActionResult> ChangePassword([FromBody] AccountDto request)
    {
        try
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthenticated();
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
            return InternalError("me/password", exception);
        }
    }

    private bool TryGetUserId(out Guid userId)
    {
        userId = _currentUser.UserId ?? Guid.Empty;
        return userId != Guid.Empty;
    }

    private UnauthorizedObjectResult Unauthenticated()
    {
        return Unauthorized(new Result
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
            new Result
            {
                Success = false,
                ErrorCode = "IDENTITY_INTERNAL_ERROR",
                Message = "Hệ thống tài khoản đang gặp lỗi. Vui lòng thử lại sau."
            });
    }
}
