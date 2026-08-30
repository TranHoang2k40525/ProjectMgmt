using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.Notification.Contracts;

namespace ProjectMgmt.Tests.Fakes;

public sealed class FakeUserLookupService : IUserLookupService
{
    private readonly Dictionary<Guid, UserDisplayInfo> _users = [];

    public FakeUserLookupService Add(UserDisplayInfo user)
    {
        _users[user.UserId] = user;
        return this;
    }

    public Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.ContainsKey(userId));

    public Task<UserDisplayInfo?> GetDisplayInfoAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.GetValueOrDefault(userId));

    public Task<IReadOnlyList<UserDisplayInfo>> GetDisplayInfoAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<UserDisplayInfo>>(userIds
            .Distinct()
            .Where(_users.ContainsKey)
            .Select(userId => _users[userId])
            .ToArray());
}

public sealed class FakeUserSkillService : IUserSkillService
{
    public List<UserSkillInfo> Skills { get; } = [];

    public List<UserProfileFeatures> Profiles { get; } = [];

    public Task<IReadOnlyList<UserSkillInfo>> GetUserSkillsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<UserSkillInfo>>(Skills
            .Where(skill => userIds.Contains(skill.UserId))
            .ToArray());

    public Task<IReadOnlyList<UserProfileFeatures>> GetProfileFeaturesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<UserProfileFeatures>>(Profiles
            .Where(profile => userIds.Contains(profile.UserId))
            .ToArray());
}

public sealed class RecordingNotificationSender : INotificationSender
{
    private readonly List<NotificationDto> _sent = [];

    public IReadOnlyList<NotificationDto> Sent => _sent.AsReadOnly();

    public Task SendAsync(NotificationDto notification, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sent)
        {
            _sent.Add(notification);
        }

        return Task.CompletedTask;
    }
}
