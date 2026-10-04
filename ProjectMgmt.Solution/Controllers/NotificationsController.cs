using IdentityExperience.Application.Dto;
using IdentityExperience.Application.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectMgmt.IdentityAccess.Contracts;

namespace ProjectMgmt.Solution.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/notifications")]
public class NotificationsController : ControllerBase
{
    private static readonly Action<ILogger, string, Exception?> LogEndpointFailure =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(2011, "NotificationEndpointFailure"),
            "Lỗi không xử lý tại endpoint thông báo {Endpoint}.");

    private readonly INotificationServices _notificationServices;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        INotificationServices notificationServices,
        ICurrentUserContext currentUser,
        ILogger<NotificationsController> logger)
    {
        _notificationServices = notificationServices;
        _currentUser = currentUser;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetInbox(
        bool? isRead,
        int page = 1,
        int pageSize = 20)
    {
        try
        {
            if (!_currentUser.UserId.HasValue)
            {
                return UnauthorizedFailure();
            }

            var result = await _notificationServices.GetInboxAsync(
                _currentUser.UserId.Value,
                isRead,
                page,
                pageSize);
            return result.Success ? Ok(result) : BadRequest(result);
        }
        catch (Exception exception)
        {
            return InternalError("inbox", exception);
        }
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        try
        {
            if (!_currentUser.UserId.HasValue)
            {
                return UnauthorizedFailure();
            }

            return Ok(await _notificationServices.GetUnreadCountAsync(
                _currentUser.UserId.Value));
        }
        catch (Exception exception)
        {
            return InternalError("unread-count", exception);
        }
    }

    [HttpPut("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid notificationId)
    {
        try
        {
            if (!_currentUser.UserId.HasValue)
            {
                return UnauthorizedFailure();
            }

            var result = await _notificationServices.MarkReadAsync(
                _currentUser.UserId.Value,
                notificationId);
            return result.Success ? Ok(result) : NotFound(result);
        }
        catch (Exception exception)
        {
            return InternalError("mark-read", exception);
        }
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        try
        {
            if (!_currentUser.UserId.HasValue)
            {
                return UnauthorizedFailure();
            }

            return Ok(await _notificationServices.MarkAllReadAsync(
                _currentUser.UserId.Value));
        }
        catch (Exception exception)
        {
            return InternalError("mark-all-read", exception);
        }
    }

    private UnauthorizedObjectResult UnauthorizedFailure()
    {
        return Unauthorized(new NotificationDto
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
            new NotificationDto
            {
                Success = false,
                ErrorCode = "NOTIFICATION_INTERNAL_ERROR",
                Message = "Hệ thống thông báo đang gặp lỗi. Vui lòng thử lại sau."
            });
    }
}
