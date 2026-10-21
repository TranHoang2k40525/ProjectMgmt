using DeliveryIntelligence.Domain.Entities;

namespace DeliveryIntelligence.Domain.IRepositories;

public interface IIssueRepository
{
    Task<Issue?> GetByIdAsync(Guid issueId, bool includeDeleted = false, CancellationToken cancellationToken = default);

    Task<Issue?> GetByNumberAsync(Guid projectId, int issueNumber, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Issue>> GetByIdsAsync(IEnumerable<Guid> issueIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Issue>> GetChildrenAsync(Guid parentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Issue>> GetByEpicAsync(Guid epicId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Issue>> GetBySprintAsync(Guid sprintId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Issue>> GetOpenByAssigneeAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IssueStatusHistory>> GetStatusHistoryAsync(Guid issueId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IssueAssignmentHistory>> GetAssignmentHistoryAsync(Guid issueId, CancellationToken cancellationToken = default);

    Task<IssuePage> ListAsync(IssueListFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Toàn bộ Issue còn hiệu lực của Project theo thứ tự rank, có tracking để rebalance.</summary>
    Task<IReadOnlyList<Issue>> GetRankedAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<decimal?> GetMaxRankAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task AddAsync(Issue issue, CancellationToken cancellationToken = default);

    Task AddActivityAsync(ActivityLog activity, CancellationToken cancellationToken = default);

    /// <summary>ActivityLog mới nhất trước (IX_ActivityLog_Issue), phân trang offset — log mỗi Issue nhỏ.</summary>
    Task<(IReadOnlyList<ActivityLog> Items, int TotalCount)> GetActivitiesAsync(
        Guid issueId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Lần đổi trạng thái gần nhất của Issue (dùng IX_IssueStatusHistory_Issue).</summary>
    Task<IssueStatusHistory?> GetLastStatusChangeAsync(Guid issueId, CancellationToken cancellationToken = default);

    Task AddStatusHistoryAsync(IssueStatusHistory history, CancellationToken cancellationToken = default);

    Task AddAssignmentHistoryAsync(IssueAssignmentHistory history, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Đọc Issue và khóa dòng (SELECT … FOR UPDATE) trong transaction hiện tại để các thay đổi trạng thái đồng thời chạy tuần tự.
    /// </summary>
    Task<Issue?> LockForUpdateAsync(Guid issueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Chạy <paramref name="work"/> trong một transaction (có execution strategy retry). Change tracker được xóa trước mỗi lần chạy,
    /// nên <paramref name="work"/> phải tự nạp lại dữ liệu nó cần.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken = default);
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
