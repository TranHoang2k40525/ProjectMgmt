using ProjectMgmt.Modules.IdentityExperience.Infrastructure.Persistence;
using ProjectMgmt.Modules.IdentityExperience.Notification.Domain.Repositories;
using NotificationEntity = ProjectMgmt.Modules.IdentityExperience.Notification.Domain.Entities.Notification;

namespace ProjectMgmt.Modules.IdentityExperience.Notification.Infrastructure.Persistence;

internal sealed class EfNotificationRepository(IdentityExperienceDbContext dbContext) : INotificationRepository
{
    public async Task AddAsync(NotificationEntity notification, CancellationToken cancellationToken = default) =>
        await dbContext.Notifications.AddAsync(notification, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.SaveChangesAsync(cancellationToken);
}
