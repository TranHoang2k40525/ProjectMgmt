namespace ProjectMgmt.Notification.Contracts;

public sealed record NotificationDto(
    Guid UserId,
    Guid? ProjectId,
    string Type,
    string Content,
    string? EntityType = null,
    Guid? EntityId = null,
    IReadOnlyDictionary<string, string>? Metadata = null,
    string? CorrelationId = null);

public interface INotificationSender
{
    Task SendAsync(NotificationDto notification, CancellationToken cancellationToken = default);
}
