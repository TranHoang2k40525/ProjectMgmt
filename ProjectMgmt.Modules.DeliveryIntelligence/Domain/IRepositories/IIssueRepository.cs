using DeliveryIntelligence.Domain.Entities;

namespace DeliveryIntelligence.Domain.IRepositories;

public interface IIssueRepository
{
    Task<Issue?> GetByIdAsync(Guid issueId, bool includeDeleted = false);

    Task<Issue?> GetByNumberAsync(Guid projectId, int issueNumber);

    Task<IReadOnlyList<Issue>> GetByIdsAsync(IEnumerable<Guid> issueIds);

    Task<IReadOnlyList<Issue>> GetChildrenAsync(Guid parentId);

    Task<IReadOnlyList<Issue>> GetByEpicAsync(Guid epicId);

    Task<IReadOnlyList<Issue>> GetBySprintAsync(Guid sprintId);

    Task<IReadOnlyList<Issue>> GetOpenByAssigneeAsync(Guid userId);

    Task<IReadOnlyList<IssueStatusHistory>> GetStatusHistoryAsync(Guid issueId);

    Task<IReadOnlyList<IssueAssignmentHistory>> GetAssignmentHistoryAsync(Guid issueId);

    Task<IssuePage> ListAsync(IssueListFilter filter);

    /// <summary>Toàn bộ Issue còn hiệu lực của Project theo thứ tự rank, có tracking để rebalance.</summary>
    Task<IReadOnlyList<Issue>> GetRankedAsync(Guid projectId);

    Task<decimal?> GetMaxRankAsync(Guid projectId);

    Task AddAsync(Issue issue);

    Task AddActivityAsync(ActivityLog activity);

    /// <summary>ActivityLog mới nhất trước (IX_ActivityLog_Issue), phân trang offset — log mỗi Issue nhỏ.</summary>
    Task<(IReadOnlyList<ActivityLog> Items, int TotalCount)> GetActivitiesAsync(
        Guid issueId,
        int page,
        int pageSize);

    /// <summary>Lần đổi trạng thái gần nhất của Issue (dùng IX_IssueStatusHistory_Issue).</summary>
    Task<IssueStatusHistory?> GetLastStatusChangeAsync(Guid issueId);

    Task AddStatusHistoryAsync(IssueStatusHistory history);

    Task AddAssignmentHistoryAsync(IssueAssignmentHistory history);

    Task SaveChangesAsync();

    /// <summary>
    /// Đọc Issue và khóa dòng (SELECT … FOR UPDATE) trong transaction hiện tại để các thay đổi trạng thái đồng thời chạy tuần tự.
    /// </summary>
    Task<Issue?> LockForUpdateAsync(Guid issueId);

    /// <summary>
    /// Chạy <paramref name="work"/> trong một transaction (có execution strategy retry). Khi chạy lại sau lỗi tạm thời,
    /// change tracker được xóa nên <paramref name="work"/> phải tự nạp lại dữ liệu nó cần.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> work);
}

public class IssueListFilter
{
    public const int MaxPageSize = 100;

    public Guid ProjectId { get; set; }
    public Guid? SprintId { get; set; }
    public bool BacklogOnly { get; set; }
    public Guid? StatusId { get; set; }
    public Guid? AssigneeId { get; set; }
    public Guid? IssueTypeId { get; set; }
    public Guid? ParentId { get; set; }
    public Guid? EpicId { get; set; }
    public string? SearchText { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class IssuePage
{
    public IssuePage(IReadOnlyList<Issue> items, int totalCount)
    {
        Items = items;
        TotalCount = totalCount;
    }

    public IReadOnlyList<Issue> Items { get; }
    public int TotalCount { get; }
}
