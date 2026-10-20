using IdentityExperience.Application.Dto;
using IdentityExperience.Application.IServices;
using IdentityExperience.Application.Services;
using IdentityExperience.Domain.Entities;
using IdentityExperience.Domain.IRepositories;
using IdentityExperience.Domain.Models;
using Xunit;

namespace ProjectMgmt.Tests;

public class NotificationTests
{
    private static readonly DateTime TestNowUtc = new(2026, 9, 30, 4, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task InboxReturnsRepositoryPageAndPagingMetadata()
    {
        var userId = Guid.NewGuid();
        var repository = new TrackingNotificationRepository
        {
            PageResult = new NotificationPage
            {
                TotalCount = 21,
                Items =
                [
                    new Notification
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        Type = NotificationTypes.ProjectInvitation,
                        Title = "Bạn đã được thêm vào dự án",
                        CreatedAt = TestNowUtc
                    }
                ]
            }
        };
        var service = CreateService(repository);

        var result = await service.GetInboxAsync(userId, false, 2, 10);

        Assert.True(result.Success);
        Assert.Equal(21, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Single(result.Items!);
        Assert.Equal(false, repository.LastIsRead);
    }

    [Fact]
    public async Task UserCannotMarkAnotherUsersNotificationAsRead()
    {
        var repository = new TrackingNotificationRepository { MarkReadResult = null };
        var service = CreateService(repository);

        var result = await service.MarkReadAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Equal("NOTIFICATION_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task PublishPersistsBeforeRealtimeAndEmailDelivery()
    {
        var repository = new TrackingNotificationRepository();
        var realtime = new TrackingRealtimeSender(repository);
        var email = new TrackingNotificationEmailDispatcher(repository, realtime);
        var service = new NotificationServices(
            repository,
            realtime,
            email,
            new FixedTimeProvider(TestNowUtc));

        var published = await service.PublishAsync(new NotificationDto
        {
            UserId = Guid.NewGuid(),
            Type = NotificationTypes.ProjectInvitation,
            Title = "Bạn đã được thêm vào dự án",
            ProjectId = Guid.NewGuid(),
            SendEmail = true
        });

        Assert.True(published);
        Assert.True(repository.Stored);
        Assert.True(realtime.ObservedStoredBeforeSend);
        Assert.True(email.ObservedRealtimeAfterStored);
    }

    [Fact]
    public async Task PublishStopsDeliveryWhenDatabasePersistenceFails()
    {
        var repository = new TrackingNotificationRepository { StoreSucceeds = false };
        var realtime = new TrackingRealtimeSender(repository);
        var email = new TrackingNotificationEmailDispatcher(repository, realtime);
        var service = new NotificationServices(
            repository,
            realtime,
            email,
            new FixedTimeProvider(TestNowUtc));

        var published = await service.PublishAsync(new NotificationDto
        {
            UserId = Guid.NewGuid(),
            Type = NotificationTypes.ProjectRoleChanged,
            Title = "Vai trò dự án đã thay đổi",
            SendEmail = true
        });

        Assert.False(published);
        Assert.False(realtime.Called);
        Assert.False(email.Called);
    }

    private static NotificationServices CreateService(TrackingNotificationRepository repository)
    {
        var realtime = new TrackingRealtimeSender(repository);
        return new NotificationServices(
            repository,
            realtime,
            new TrackingNotificationEmailDispatcher(repository, realtime),
            new FixedTimeProvider(TestNowUtc));
    }
}

public class TrackingNotificationRepository : INotificationRepository
{
    public NotificationPage PageResult { get; set; } = new();
    public Notification? MarkReadResult { get; set; }
    public bool StoreSucceeds { get; set; } = true;
    public bool Stored { get; private set; }
    public bool? LastIsRead { get; private set; }

    public Task<NotificationPage> GetInboxAsync(
        Guid userId,
        bool? isRead,
        int page,
        int pageSize)
    {
        LastIsRead = isRead;
        return Task.FromResult(PageResult);
    }

    public Task<int> GetUnreadCountAsync(Guid userId)
    {
        return Task.FromResult(0);
    }

    public Task<Notification?> MarkReadAsync(
        Guid userId,
        Guid notificationId,
        DateTime readAtUtc)
    {
        return Task.FromResult(MarkReadResult);
    }

    public Task<int> MarkAllReadAsync(Guid userId, DateTime readAtUtc)
    {
        return Task.FromResult(0);
    }

    public Task<bool> TryAddAsync(Notification notification)
    {
        Stored = StoreSucceeds;
        return Task.FromResult(StoreSucceeds);
    }
}

public class TrackingRealtimeSender : INotificationRealtimeSender
{
    private readonly TrackingNotificationRepository _repository;

    public TrackingRealtimeSender(TrackingNotificationRepository repository)
    {
        _repository = repository;
    }

    public bool Called { get; private set; }
    public bool ObservedStoredBeforeSend { get; private set; }

    public Task<bool> SendToUserAsync(Guid userId, NotificationDto notification)
    {
        Called = true;
        ObservedStoredBeforeSend = _repository.Stored;
        return Task.FromResult(true);
    }
}

public class TrackingNotificationEmailDispatcher : INotificationEmailDispatcher
{
    private readonly TrackingNotificationRepository _repository;
    private readonly TrackingRealtimeSender _realtime;

    public TrackingNotificationEmailDispatcher(
        TrackingNotificationRepository repository,
        TrackingRealtimeSender realtime)
    {
        _repository = repository;
        _realtime = realtime;
    }

    public bool Called { get; private set; }
    public bool ObservedRealtimeAfterStored { get; private set; }

    public Task<bool> SendAsync(NotificationDto notification)
    {
        Called = true;
        ObservedRealtimeAfterStored = _repository.Stored && _realtime.Called;
        return Task.FromResult(true);
    }
}
