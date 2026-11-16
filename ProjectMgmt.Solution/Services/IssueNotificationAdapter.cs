using DeliveryIntelligence.Application.IServices;
using IdentityExperience.Application.Dto;
using IdentityExperience.Application.IServices;

namespace ProjectMgmt.Solution.Services;

/// <summary>
/// Nối port IIssueNotifier của DeliveryIntelligence với inbox/realtime của IdentityExperience.
/// Mỗi người nhận một bản ghi Notification; lỗi gửi chỉ ghi log, không làm hỏng thao tác đã commit.
/// </summary>
public class IssueNotificationAdapter : IIssueNotifier
{
    private static readonly Action<ILogger, string, Guid, Exception?> LogFailure =
        LoggerMessage.Define<string, Guid>(
            LogLevel.Warning,
            new EventId(3101, "IssueNotificationFailed"),
            "Không gửi được thông báo {Type} cho Issue {IssueId}.");

    private readonly INotificationServices _notifications;
    private readonly ILogger<IssueNotificationAdapter> _logger;

    public IssueNotificationAdapter(INotificationServices notifications, ILogger<IssueNotificationAdapter> logger)
    {
        _notifications = notifications;
        _logger = logger;
    }

    public async Task NotifyAsync(IssueNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        foreach (var recipient in notification.RecipientUserIds)
        {
            try
            {
                var published = await _notifications.PublishAsync(new NotificationDto
                {
                    UserId = recipient,
                    Type = notification.Type,
                    Title = notification.Title.Length > 255 ? notification.Title[..255] : notification.Title,
                    Content = notification.Content,
                    EntityType = "Issue",
                    EntityId = notification.IssueId,
                    ProjectId = notification.ProjectId,
                    ActorId = notification.ActorUserId
                });
                if (!published)
                {
                    LogFailure(_logger, notification.Type, notification.IssueId, null);
                }
            }
#pragma warning disable CA1031 // Thông báo là kênh phụ: mọi lỗi đều phải được nuốt sau khi ghi log.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                LogFailure(_logger, notification.Type, notification.IssueId, exception);
            }
        }
    }
}
