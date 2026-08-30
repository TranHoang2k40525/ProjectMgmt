using ProjectMgmt.BuildingBlocks.Common;
using ProjectMgmt.BuildingBlocks.Results;
using ProjectMgmt.IssueTracking.Contracts;

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

public sealed class FakeIssueReadService : IIssueReadService
{
    public Dictionary<Guid, IssueDto> Issues { get; } = [];

    public List<IssueStatusHistoryDto> StatusHistory { get; } = [];

    public List<IssueAssignmentHistoryDto> AssignmentHistory { get; } = [];

    public Task<Result<IssueDto>> GetByIdAsync(Guid issueId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Issues.TryGetValue(issueId, out var issue)
            ? Result.Success(issue)
            : Result.Failure<IssueDto>(Error.NotFound("issue.not_found", "Issue was not found.")));

    public Task<PagedResult<IssueDto>> GetBySprintAsync(
        Guid sprintId,
        PageRequest page,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ToPage(Issues.Values.Where(issue => issue.SprintId == sprintId), page));

    public Task<PagedResult<IssueDto>> GetOpenByAssigneeAsync(
        Guid assigneeId,
        PageRequest page,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ToPage(Issues.Values.Where(issue => issue.AssigneeId == assigneeId), page));

    public Task<IReadOnlyList<IssueStatusHistoryDto>> GetStatusHistoryAsync(
        Guid issueId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IssueStatusHistoryDto>>(StatusHistory.Where(item => item.IssueId == issueId).ToArray());

    public Task<IReadOnlyList<IssueAssignmentHistoryDto>> GetAssignmentHistoryAsync(
        Guid issueId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IssueAssignmentHistoryDto>>(AssignmentHistory.Where(item => item.IssueId == issueId).ToArray());

    private static PagedResult<IssueDto> ToPage(IEnumerable<IssueDto> query, PageRequest page)
    {
        var ordered = query.OrderBy(issue => issue.IssueNumber).ToArray();
        return new PagedResult<IssueDto>(ordered.Skip(page.Skip).Take(page.PageSize).ToArray(), page.PageNumber, page.PageSize, ordered.Length);
    }
}

public sealed class FakeIssueSprintService : IIssueSprintService
{
    public Dictionary<Guid, Guid?> SprintByIssue { get; } = [];

    public Task<Result> MoveToSprintAsync(
        IReadOnlyCollection<Guid> issueIds,
        Guid sprintId,
        CancellationToken cancellationToken = default)
    {
        foreach (var issueId in issueIds)
        {
            SprintByIssue[issueId] = sprintId;
        }

        return Task.FromResult(Result.Success());
    }

    public Task<Result> ReturnToBacklogAsync(
        IReadOnlyCollection<Guid> issueIds,
        CancellationToken cancellationToken = default)
    {
        foreach (var issueId in issueIds)
        {
            SprintByIssue[issueId] = null;
        }

        return Task.FromResult(Result.Success());
    }
}

public sealed class FakeIssueSkillService : IIssueSkillService
{
    public Dictionary<Guid, IReadOnlyList<IssueRequiredSkillDto>> SkillsByIssue { get; } = [];

    public Task<IReadOnlyDictionary<Guid, IReadOnlyList<IssueRequiredSkillDto>>> GetRequiredSkillsAsync(
        IReadOnlyCollection<Guid> issueIds,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<IssueRequiredSkillDto>>>(issueIds
            .Distinct()
            .ToDictionary(issueId => issueId, issueId => SkillsByIssue.GetValueOrDefault(issueId) ?? []));
}
