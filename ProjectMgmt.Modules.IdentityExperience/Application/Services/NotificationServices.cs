using IdentityExperience.Application.Dto;
using IdentityExperience.Application.IServices;
using IdentityExperience.Domain.Entities;
using IdentityExperience.Domain.IRepositories;

namespace IdentityExperience.Application.Services;

public class NotificationServices : INotificationServices
{
    private const int MaximumPageSize = 100;
    private readonly INotificationRepository _notificationRepository;
    private readonly INotificationRealtimeSender _realtimeSender;
    private readonly INotificationEmailDispatcher _emailDispatcher;
    private readonly TimeProvider _timeProvider;

    public NotificationServices(
        INotificationRepository notificationRepository,
        INotificationRealtimeSender realtimeSender,
        INotificationEmailDispatcher emailDispatcher,
        TimeProvider timeProvider)
    {
        _notificationRepository = notificationRepository;
        _realtimeSender = realtimeSender;
        _emailDispatcher = emailDispatcher;
        _timeProvider = timeProvider;
    }

    public async Task<NotificationDto> GetInboxAsync(
        Guid userId,
        bool? isRead,
        int page,
        int pageSize)
    {
        if (userId == Guid.Empty || page < 1 || pageSize is < 1 or > MaximumPageSize)
        {
            return Failure(
                "NOTIFICATION_QUERY_INVALID",
                "Trang phải từ 1 và số bản ghi mỗi trang phải từ 1 đến 100.");
        }

        var result = await _notificationRepository.GetInboxAsync(
            userId,
            isRead,
            page,
            pageSize);
        return new NotificationDto
        {
            Success = true,
            Items = result.Items.Select(Map).ToList(),
            TotalCount = result.TotalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<NotificationDto> GetUnreadCountAsync(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return Failure("NOTIFICATION_USER_INVALID", "Phiên đăng nhập không hợp lệ.");
        }

        return new NotificationDto
        {
            Success = true,
            UnreadCount = await _notificationRepository.GetUnreadCountAsync(userId)
        };
    }

    public async Task<NotificationDto> MarkReadAsync(Guid userId, Guid notificationId)
    {
        if (userId == Guid.Empty || notificationId == Guid.Empty)
        {
            return Failure("NOTIFICATION_ID_INVALID", "Mã thông báo không hợp lệ.");
        }

        var notification = await _notificationRepository.MarkReadAsync(
            userId,
            notificationId,
            _timeProvider.GetUtcNow().UtcDateTime);
        if (notification is null)
        {
            return Failure(
                "NOTIFICATION_NOT_FOUND",
                "Không tìm thấy thông báo của người dùng hiện tại.");
        }

        var response = Map(notification);
        response.Success = true;
        response.Message = "Đã đánh dấu thông báo là đã đọc.";
        return response;
    }

    public async Task<NotificationDto> MarkAllReadAsync(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return Failure("NOTIFICATION_USER_INVALID", "Phiên đăng nhập không hợp lệ.");
        }

        var updatedCount = await _notificationRepository.MarkAllReadAsync(
            userId,
            _timeProvider.GetUtcNow().UtcDateTime);
        return new NotificationDto
        {
            Success = true,
            Message = "Đã đánh dấu toàn bộ thông báo là đã đọc.",
            UnreadCount = 0,
            TotalCount = updatedCount
        };
    }

    public async Task<bool> PublishAsync(NotificationDto notification)
    {
        if (!IsPublishRequestValid(notification))
        {
            return false;
        }

        var entity = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = notification.UserId!.Value,
            Type = notification.Type!.Trim(),
            Title = notification.Title!.Trim(),
            Content = OptionalText(notification.Content),
            EntityType = OptionalText(notification.EntityType),
            EntityId = notification.EntityId,
            ProjectId = notification.ProjectId,
            ActorId = notification.ActorId,
            IsRead = false,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        if (!await _notificationRepository.TryAddAsync(entity))
        {
            return false;
        }

        var deliveredNotification = Map(entity);
        deliveredNotification.RoleName = notification.RoleName;
        await _realtimeSender.SendToUserAsync(entity.UserId, deliveredNotification);
        if (notification.SendEmail)
        {
            await _emailDispatcher.SendAsync(deliveredNotification);
        }

        return true;
    }

    private static bool IsPublishRequestValid(NotificationDto notification)
    {
        return notification.UserId.HasValue
            && notification.UserId.Value != Guid.Empty
            && !string.IsNullOrWhiteSpace(notification.Type)
            && notification.Type.Trim().Length <= 50
            && !string.IsNullOrWhiteSpace(notification.Title)
            && notification.Title.Trim().Length <= 255
            && (notification.EntityType is null || notification.EntityType.Trim().Length <= 50);
    }

    private static NotificationDto Map(Notification notification)
    {
        return new NotificationDto
        {
            Success = true,
            NotificationId = notification.Id,
            UserId = notification.UserId,
            Type = notification.Type,
            Title = notification.Title,
            Content = notification.Content,
            EntityType = notification.EntityType,
            EntityId = notification.EntityId,
            ProjectId = notification.ProjectId,
            ActorId = notification.ActorId,
            IsRead = notification.IsRead,
            ReadAt = notification.ReadAt,
            CreatedAt = notification.CreatedAt
        };
    }

    private static NotificationDto Failure(string errorCode, string message)
    {
        return new NotificationDto
        {
            Success = false,
            ErrorCode = errorCode,
            Message = message
        };
    }

    private static string? OptionalText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
