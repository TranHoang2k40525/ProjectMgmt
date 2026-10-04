using IdentityExperience.Domain.Entities;
using IdentityExperience.Domain.IRepositories;
using IdentityExperience.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IdentityExperience.Infrastructure.Repository;

public class NotificationRepository : INotificationRepository
{
    private static readonly Action<ILogger, Guid, string, Exception?> LogNotificationPersistenceFailure =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Error,
            new EventId(1010, "NotificationPersistenceFailed"),
            "Không thể lưu thông báo {NotificationId} loại {NotificationType}.");

    private readonly IdentityExperienceDbContext _context;
    private readonly ILogger<NotificationRepository> _logger;

    public NotificationRepository(
        IdentityExperienceDbContext context,
        ILogger<NotificationRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<NotificationPage> GetInboxAsync(
        Guid userId,
        bool? isRead,
        int page,
        int pageSize)
    {
        var query = _context.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId);
        if (isRead.HasValue)
        {
            query = query.Where(notification => notification.IsRead == isRead.Value);
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new NotificationPage
        {
            Items = items,
            TotalCount = totalCount
        };
    }

    public Task<int> GetUnreadCountAsync(Guid userId)
    {
        return _context.Notifications
            .AsNoTracking()
            .CountAsync(notification => notification.UserId == userId && !notification.IsRead);
    }

    public async Task<Notification?> MarkReadAsync(
        Guid userId,
        Guid notificationId,
        DateTime readAtUtc)
    {
        var notification = await _context.Notifications.FirstOrDefaultAsync(candidate =>
            candidate.Id == notificationId && candidate.UserId == userId);
        if (notification is null)
        {
            return null;
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = readAtUtc;
            await _context.SaveChangesAsync();
        }

        return notification;
    }

    public Task<int> MarkAllReadAsync(Guid userId, DateTime readAtUtc)
    {
        return _context.Notifications
            .Where(notification => notification.UserId == userId && !notification.IsRead)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(notification => notification.IsRead, true)
                .SetProperty(notification => notification.ReadAt, readAtUtc));
    }

    public async Task<bool> TryAddAsync(Notification notification)
    {
        try
        {
            await _context.Notifications.AddAsync(notification);
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception exception)
        {
            LogNotificationPersistenceFailure(
                _logger,
                notification.Id,
                notification.Type,
                exception);
            _context.ChangeTracker.Clear();
            return false;
        }
    }
}
