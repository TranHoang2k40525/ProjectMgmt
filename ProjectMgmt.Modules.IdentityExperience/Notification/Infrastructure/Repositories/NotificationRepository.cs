using ProjectMgmt.Modules.IdentityExperience.Infrastructure.Persistence;
using ProjectMgmt.Modules.IdentityExperience.Notification.Domain.IRepositories;
using NotificationEntity = ProjectMgmt.Modules.IdentityExperience.Notification.Domain.Entities.Notification;

namespace ProjectMgmt.Modules.IdentityExperience.Notification.Infrastructure.Repositories;

internal class NotificationRepository : INotificationRepository
{
    private readonly IdentityExperienceAppDbContext _dbContext;

    public NotificationRepository(IdentityExperienceAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    public async Task AddAsync(NotificationEntity notification, CancellationToken cancellationToken = default) =>
        await _dbContext.Notifications.AddAsync(notification, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.SaveChangesAsync(cancellationToken);
}
