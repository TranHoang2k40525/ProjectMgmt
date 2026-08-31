using ProjectMgmt.Notification.Contracts;
using ProjectMgmt.Modules.IdentityExperience.Notification.Domain.Repositories;
using NotificationEntity = ProjectMgmt.Modules.IdentityExperience.Notification.Domain.Entities.Notification;

namespace ProjectMgmt.Modules.IdentityExperience.Notification.Application.Services;

internal sealed class NotificationSender(INotificationRepository repository) : INotificationSender
{
    public async Task SendAsync(NotificationDto notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var title = notification.Content.Length <= 255
            ? notification.Content
            : notification.Content[..255];

        var entity = new NotificationEntity
        {
            Id = Guid.NewGuid(),
            UserId = notification.UserId,
            ProjectId = notification.ProjectId,
            Type = notification.Type,
            Title = title,
            Content = notification.Content,
            EntityType = notification.EntityType,
            EntityId = notification.EntityId,
            CreatedAt = DateTime.UtcNow
        };

        await repository.AddAsync(entity, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
