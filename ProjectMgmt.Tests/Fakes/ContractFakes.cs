using System.Collections.Concurrent;
using ProjectMgmt.BuildingBlocks.Results;
using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.IssueTracking.Contracts;
using ProjectMgmt.ProjectManagement.Contracts;

namespace ProjectMgmt.Tests.Fakes;

public sealed class FakeIssueService : IIssueService
{
    private int _nextIssueNumber;

    public List<CreateIssueDto> Requests { get; } = [];

    public Func<CreateIssueDto, Result<CreateIssueResultDto>>? Handler { get; set; }

    public Task<Result<CreateIssueResultDto>> CreateIssueAsync(
        CreateIssueDto request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);
        var number = Interlocked.Increment(ref _nextIssueNumber);
        var result = Handler?.Invoke(request)
            ?? Result.Success(new CreateIssueResultDto(DeterministicGuid(number), number, $"FAKE-{number}"));
        return Task.FromResult(result);
    }

    private static Guid DeterministicGuid(int value)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, value);
        return new Guid(bytes);
    }
}

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

public sealed class FakeProjectLookupService : IProjectLookupService
{
    public Dictionary<Guid, string> ProjectKeys { get; } = [];
    public Dictionary<Guid, IReadOnlyList<ProjectIssueTypeDto>> IssueTypes { get; } = [];
    public Dictionary<Guid, IReadOnlyList<ProjectStatusDto>> Statuses { get; } = [];

    public Task<bool> ExistsAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ProjectKeys.ContainsKey(projectId));

    public Task<string?> GetProjectKeyAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ProjectKeys.GetValueOrDefault(projectId));

    public Task<IReadOnlyList<ProjectIssueTypeDto>> GetIssueTypesAsync(
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(IssueTypes.GetValueOrDefault(projectId) ?? []);

    public Task<IReadOnlyList<ProjectStatusDto>> GetStatusesAsync(
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Statuses.GetValueOrDefault(projectId) ?? []);
}

public sealed class FakeIssueNumberGenerator : IIssueNumberGenerator
{
    private readonly ConcurrentDictionary<Guid, int> _counters = new();

    public Task<int> NextAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_counters.AddOrUpdate(projectId, 1, (_, current) => checked(current + 1)));
    }
}
