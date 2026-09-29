using IdentityExperience.Application.Dto;
using IdentityExperience.Application.IServices;
using IdentityExperience.Domain.IRepositories;
using IdentityExperience.Domain.Models;
using Microsoft.Extensions.Logging;

namespace IdentityExperience.Infrastructure.Services;

public class NotificationEmailDispatcher : INotificationEmailDispatcher
{
    private static readonly Action<ILogger, Guid, string, Exception?> LogDispatchFailure =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Error,
            new EventId(1011, "NotificationEmailDispatchFailed"),
            "Không thể chuẩn bị email thông báo cho người dùng {UserId}, loại {NotificationType}.");

    private readonly IIdentityRepository _identityRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<NotificationEmailDispatcher> _logger;
    private readonly TimeProvider _timeProvider;

    public NotificationEmailDispatcher(
        IIdentityRepository identityRepository,
        IEmailService emailService,
        ILogger<NotificationEmailDispatcher> logger,
        TimeProvider timeProvider)
    {
        _identityRepository = identityRepository;
        _emailService = emailService;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task<bool> SendAsync(NotificationDto notification)
    {
        if (!notification.UserId.HasValue)
        {
            return false;
        }

        try
        {
            var user = await _identityRepository.GetUserByIdAsync(notification.UserId.Value);
            if (user is null || !user.IsActive || !user.IsEmailVerified)
            {
                return false;
            }

            var profile = await _identityRepository.GetUserProfileAsync(notification.UserId.Value);
            var recipientName = profile?.DisplayName ?? user.Email;
            if (notification.Type == NotificationTypes.PasswordChanged)
            {
                return await _emailService.SendPasswordChangedAsync(
                    user.Email,
                    recipientName,
                    notification.CreatedAt ?? _timeProvider.GetUtcNow().UtcDateTime);
            }

            if (!notification.ProjectId.HasValue)
            {
                return false;
            }

            return notification.Type switch
            {
                NotificationTypes.ProjectInvitation => await _emailService.SendProjectInvitationAsync(
                    user.Email,
                    recipientName,
                    notification.ProjectId.Value,
                    notification.RoleName),
                NotificationTypes.ProjectRoleChanged => await _emailService.SendProjectRoleChangedAsync(
                    user.Email,
                    recipientName,
                    notification.ProjectId.Value,
                    notification.RoleName),
                NotificationTypes.ProjectRoleRevoked => await _emailService.SendProjectRoleRevokedAsync(
                    user.Email,
                    recipientName,
                    notification.ProjectId.Value),
                _ => false
            };
        }
        catch (Exception exception)
        {
            LogDispatchFailure(
                _logger,
                notification.UserId.Value,
                notification.Type ?? "Unknown",
                exception);
            return false;
        }
    }
}
