using IdentityExperience.Application.Dto;

namespace IdentityExperience.Application.IServices;

public interface INotificationRealtimeSender
{
    Task<bool> SendToUserAsync(Guid userId, NotificationDto notification);
}

public interface INotificationEmailDispatcher
{
    Task<bool> SendAsync(NotificationDto notification);
}
