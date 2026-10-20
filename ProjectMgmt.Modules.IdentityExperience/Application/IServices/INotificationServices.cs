using IdentityExperience.Application.Dto;

namespace IdentityExperience.Application.IServices;

public interface INotificationServices
{
    Task<NotificationDto> GetInboxAsync(
        Guid userId,
        bool? isRead,
        int page,
        int pageSize);

    Task<NotificationDto> GetUnreadCountAsync(Guid userId);

    Task<NotificationDto> MarkReadAsync(Guid userId, Guid notificationId);

    Task<NotificationDto> MarkAllReadAsync(Guid userId);

    Task<bool> PublishAsync(NotificationDto notification);
}
