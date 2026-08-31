namespace ProjectMgmt.Notification.Contracts;

public class NotificationDto
{
    public NotificationDto(
        Guid userId,
        Guid? projectId,
        string type,
        string content,
        string? entityType = null,
        Guid? entityId = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        string? correlationId = null)
    {
        UserId = userId;
        ProjectId = projectId;
        Type = type;
        Content = content;
        EntityType = entityType;
        EntityId = entityId;
        Metadata = metadata;
        CorrelationId = correlationId;
    }

    public Guid UserId { get; set; }
    public Guid? ProjectId { get; set; }
    public string Type { get; set; }
    public string Content { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public IReadOnlyDictionary<string, string>? Metadata { get; set; }
    public string? CorrelationId { get; set; }
}

public interface INotificationSender
{
    Task SendAsync(NotificationDto notification, CancellationToken cancellationToken = default);
}
