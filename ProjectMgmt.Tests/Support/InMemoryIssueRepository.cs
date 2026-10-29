using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Domain.IRepositories;

namespace ProjectMgmt.Tests.Support;

/// <summary>Fake IIssueRepository trên danh sách trong bộ nhớ, bám đúng thứ tự/lọc/phân trang của IssueRepository.</summary>
public class InMemoryIssueRepository : IIssueRepository
{
    public InMemoryIssueRepository(IEnumerable<Issue>? seed = null)
    {
        Issues = seed?.ToList() ?? SharedTestData.CreateIssues();
    }

    public List<Issue> Issues { get; }
    public int SaveCount { get; private set; }

    public Task<Issue?> GetByIdAsync(Guid issueId, bool includeDeleted = false) =>
        Task.FromResult(Issues.SingleOrDefault(issue => issue.Id == issueId && (includeDeleted || !issue.IsDeleted)));

    public Task<Issue?> GetByNumberAsync(Guid projectId, int issueNumber) =>
        Task.FromResult(Issues.SingleOrDefault(issue =>
            issue.ProjectId == projectId && issue.IssueNumber == issueNumber && !issue.IsDeleted));

    public Task<IReadOnlyList<Issue>> GetByIdsAsync(IEnumerable<Guid> issueIds)
    {
        var ids = issueIds.ToHashSet();
        IReadOnlyList<Issue> result = Issues.Where(issue => ids.Contains(issue.Id) && !issue.IsDeleted).ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<Issue>> GetChildrenAsync(Guid parentId)
    {
        IReadOnlyList<Issue> result = Ordered(Issues.Where(issue => issue.ParentId == parentId && !issue.IsDeleted)).ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<Issue>> GetByEpicAsync(Guid epicId)
    {
        IReadOnlyList<Issue> result = Issues.Where(issue => issue.EpicId == epicId).ToList();
        return Task.FromResult(result);
    }

    public List<IssueStatusHistory> StatusHistory { get; } = [];
    public List<IssueAssignmentHistory> AssignmentHistory { get; } = [];

    public Task<IReadOnlyList<Issue>> GetBySprintAsync(Guid sprintId)
    {
        IReadOnlyList<Issue> result = Ordered(Issues.Where(issue => issue.SprintId == sprintId && !issue.IsDeleted)).ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<Issue>> GetOpenByAssigneeAsync(Guid userId)
    {
        IReadOnlyList<Issue> result = Issues
            .Where(issue => issue.AssigneeId == userId && !issue.IsDeleted && issue.ResolvedAt is null)
            .OrderBy(issue => issue.DueDate is null)
            .ThenBy(issue => issue.DueDate)
            .ThenBy(issue => issue.RankOrder)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<IssueStatusHistory>> GetStatusHistoryAsync(Guid issueId)
    {
        IReadOnlyList<IssueStatusHistory> result = StatusHistory.Where(h => h.IssueId == issueId).OrderBy(h => h.ChangedAt).ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<IssueAssignmentHistory>> GetAssignmentHistoryAsync(Guid issueId)
    {
        IReadOnlyList<IssueAssignmentHistory> result = AssignmentHistory.Where(h => h.IssueId == issueId).OrderBy(h => h.AssignedAt).ToList();
        return Task.FromResult(result);
    }

    public Task<IssuePage> ListAsync(IssueListFilter filter)
    {
        var page = Math.Max(filter.Page, 1);
        var pageSize = Math.Clamp(filter.PageSize, 1, IssueListFilter.MaxPageSize);
        var query = Issues.Where(issue => issue.ProjectId == filter.ProjectId && !issue.IsDeleted);
        if (filter.BacklogOnly)
        {
            query = query.Where(issue => issue.SprintId is null);
        }
        else if (filter.SprintId.HasValue)
        {
            query = query.Where(issue => issue.SprintId == filter.SprintId);
        }

        if (filter.StatusId.HasValue) query = query.Where(issue => issue.StatusId == filter.StatusId);
        if (filter.AssigneeId.HasValue) query = query.Where(issue => issue.AssigneeId == filter.AssigneeId);
        if (filter.IssueTypeId.HasValue) query = query.Where(issue => issue.IssueTypeId == filter.IssueTypeId);
        if (filter.ParentId.HasValue) query = query.Where(issue => issue.ParentId == filter.ParentId);
        if (filter.EpicId.HasValue) query = query.Where(issue => issue.EpicId == filter.EpicId);
        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var text = filter.SearchText.Trim();
            var hasNumber = int.TryParse(text, out var number);
            query = query.Where(issue =>
                issue.Title.Contains(text, StringComparison.OrdinalIgnoreCase) || (hasNumber && issue.IssueNumber == number));
        }

        var matched = Ordered(query).ToList();
        IReadOnlyList<Issue> items = matched.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult(new IssuePage(items, matched.Count));
    }

    public Task<IReadOnlyList<Issue>> GetRankedAsync(Guid projectId)
    {
        IReadOnlyList<Issue> result = Ordered(Issues.Where(issue => issue.ProjectId == projectId && !issue.IsDeleted)).ToList();
        return Task.FromResult(result);
    }

    public Task<decimal?> GetMaxRankAsync(Guid projectId) =>
        Task.FromResult(Issues.Where(issue => issue.ProjectId == projectId && !issue.IsDeleted)
            .Select(issue => (decimal?)issue.RankOrder)
            .Max());

    public Task AddAsync(Issue issue)
    {
        Issues.Add(issue);
        return Task.CompletedTask;
    }

    public List<ActivityLog> Activities { get; } = [];

    public Task AddActivityAsync(ActivityLog activity)
    {
        Activities.Add(activity);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync()
    {
        SaveCount++;
        return Task.CompletedTask;
    }

    public Task<(IReadOnlyList<ActivityLog> Items, int TotalCount)> GetActivitiesAsync(
        Guid issueId,
        int page,
        int pageSize)
    {
        var matched = Activities.Where(a => a.IssueId == issueId).OrderByDescending(a => a.CreatedAt).ThenBy(a => a.Id).ToList();
        IReadOnlyList<ActivityLog> items = matched.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult((items, matched.Count));
    }

    public Task<IssueStatusHistory?> GetLastStatusChangeAsync(Guid issueId) =>
        Task.FromResult(StatusHistory.Where(h => h.IssueId == issueId).MaxBy(h => h.ChangedAt));

    public Task AddStatusHistoryAsync(IssueStatusHistory history)
    {
        StatusHistory.Add(history);
        return Task.CompletedTask;
    }

    public Task AddAssignmentHistoryAsync(IssueAssignmentHistory history)
    {
        AssignmentHistory.Add(history);
        return Task.CompletedTask;
    }

    public int TransactionCount { get; private set; }

    public Task<Issue?> LockForUpdateAsync(Guid issueId) =>
        GetByIdAsync(issueId);

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> work)
    {
        TransactionCount++;
        return await work();
    }

    private static IOrderedEnumerable<Issue> Ordered(IEnumerable<Issue> issues) =>
        issues.OrderBy(issue => issue.RankOrder).ThenBy(issue => issue.IssueNumber);
}
