using IdentityExperience.Domain.Entities;
using IdentityExperience.Domain.Models;

namespace IdentityExperience.Domain.IRepositories;

public interface INotificationRepository
{
    Task<NotificationPage> GetInboxAsync(
        Guid userId,
        bool? isRead,
        int page,
        int pageSize);

    Task<int> GetUnreadCountAsync(Guid userId);

    Task<Notification?> MarkReadAsync(
        Guid userId,
        Guid notificationId,
        DateTime readAtUtc);

    Task<int> MarkAllReadAsync(Guid userId, DateTime readAtUtc);

    Task<bool> TryAddAsync(Notification notification);
}
