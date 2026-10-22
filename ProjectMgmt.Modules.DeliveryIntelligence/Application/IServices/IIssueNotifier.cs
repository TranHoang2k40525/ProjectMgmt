namespace DeliveryIntelligence.Application.IServices;

/// <summary>Sự kiện Issue cần báo cho người liên quan (người xử lý, người tạo, người theo dõi, người được nhắc).</summary>
public record IssueNotification(
    Guid IssueId,
    Guid ProjectId,
    Guid ActorUserId,
    string Type,
    string Title,
    string? Content,
    IReadOnlyCollection<Guid> RecipientUserIds);

/// <summary>
/// Port gửi thông báo, gọi SAU khi commit. Adapter ở Host chuyển sang module thông báo của IdentityExperience.
/// Lỗi gửi thông báo không được làm hỏng thao tác nghiệp vụ đã commit.
/// </summary>
public interface IIssueNotifier
{
    Task NotifyAsync(IssueNotification notification, CancellationToken cancellationToken = default);
}

public static class IssueNotificationTypes
{
    public const string StatusChanged = "IssueStatusChanged";
    public const string Assigned = "IssueAssigned";
    public const string Mentioned = "IssueMentioned";
    public const string Commented = "IssueCommented";
}
