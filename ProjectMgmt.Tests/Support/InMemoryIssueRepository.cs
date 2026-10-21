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

    public Task<Issue?> GetByIdAsync(Guid issueId, bool includeDeleted = false, CancellationToken cancellationToken = default) =>
        Task.FromResult(Issues.SingleOrDefault(issue => issue.Id == issueId && (includeDeleted || !issue.IsDeleted)));

    public Task<Issue?> GetByNumberAsync(Guid projectId, int issueNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult(Issues.SingleOrDefault(issue =>
            issue.ProjectId == projectId && issue.IssueNumber == issueNumber && !issue.IsDeleted));

    public Task<IReadOnlyList<Issue>> GetByIdsAsync(IEnumerable<Guid> issueIds, CancellationToken cancellationToken = default)
    {
        var ids = issueIds.ToHashSet();
        IReadOnlyList<Issue> result = Issues.Where(issue => ids.Contains(issue.Id) && !issue.IsDeleted).ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<Issue>> GetChildrenAsync(Guid parentId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Issue> result = Ordered(Issues.Where(issue => issue.ParentId == parentId && !issue.IsDeleted)).ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<Issue>> GetByEpicAsync(Guid epicId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Issue> result = Issues.Where(issue => issue.EpicId == epicId).ToList();
        return Task.FromResult(result);
    }

    public Task<IssuePage> ListAsync(IssueListFilter filter, CancellationToken cancellationToken = default)
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

    public Task<IReadOnlyList<Issue>> GetRankedAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Issue> result = Ordered(Issues.Where(issue => issue.ProjectId == projectId && !issue.IsDeleted)).ToList();
        return Task.FromResult(result);
    }

    public Task<decimal?> GetMaxRankAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Issues.Where(issue => issue.ProjectId == projectId && !issue.IsDeleted)
            .Select(issue => (decimal?)issue.RankOrder)
            .Max());

    public Task AddAsync(Issue issue, CancellationToken cancellationToken = default)
    {
        Issues.Add(issue);
        return Task.CompletedTask;
    }

    public List<ActivityLog> Activities { get; } = [];

    public Task AddActivityAsync(ActivityLog activity, CancellationToken cancellationToken = default)
    {
        Activities.Add(activity);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }

    private static IOrderedEnumerable<Issue> Ordered(IEnumerable<Issue> issues) =>
        issues.OrderBy(issue => issue.RankOrder).ThenBy(issue => issue.IssueNumber);
}
