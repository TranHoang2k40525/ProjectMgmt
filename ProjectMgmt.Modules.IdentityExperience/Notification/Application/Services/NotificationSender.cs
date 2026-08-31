using ProjectMgmt.Notification.Contracts;
using ProjectMgmt.Modules.IdentityExperience.Notification.Domain.IRepositories;
using NotificationEntity = ProjectMgmt.Modules.IdentityExperience.Notification.Domain.Entities.Notification;

namespace ProjectMgmt.Modules.IdentityExperience.Notification.Application.Services;

internal class NotificationSender : INotificationSender
{
    private readonly INotificationRepository _repository;

    public NotificationSender(INotificationRepository repository)
    {
        _repository = repository;
    }

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

        await _repository.AddAsync(entity, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
    }
}
