using NotificationEntity = ProjectMgmt.Modules.IdentityExperience.Notification.Domain.Entities.Notification;

namespace ProjectMgmt.Modules.IdentityExperience.Notification.Domain.Repositories;

public interface INotificationRepository
{
    Task AddAsync(NotificationEntity notification, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
