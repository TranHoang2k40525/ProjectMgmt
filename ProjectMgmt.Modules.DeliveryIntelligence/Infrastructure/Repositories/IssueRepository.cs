using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Domain.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace DeliveryIntelligence.Infrastructure.Repositories;

public class IssueRepository : IIssueRepository
{
    private readonly DeliveryIntelligenceDbContext _context;

    public IssueRepository(DeliveryIntelligenceDbContext context)
    {
        _context = context;
    }

    public Task<Issue?> GetByIdAsync(Guid issueId, bool includeDeleted = false, CancellationToken cancellationToken = default) =>
        _context.Issues.SingleOrDefaultAsync(
            issue => issue.Id == issueId && (includeDeleted || !issue.IsDeleted),
            cancellationToken);

    public Task<Issue?> GetByNumberAsync(Guid projectId, int issueNumber, CancellationToken cancellationToken = default) =>
        _context.Issues.SingleOrDefaultAsync(
            issue => issue.ProjectId == projectId && issue.IssueNumber == issueNumber && !issue.IsDeleted,
            cancellationToken);

    public async Task<IReadOnlyList<Issue>> GetByIdsAsync(IEnumerable<Guid> issueIds, CancellationToken cancellationToken = default)
    {
        var ids = issueIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await _context.Issues
            .Where(issue => ids.Contains(issue.Id) && !issue.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Issue>> GetChildrenAsync(Guid parentId, CancellationToken cancellationToken = default) =>
        await _context.Issues
            .Where(issue => issue.ParentId == parentId && !issue.IsDeleted)
            .OrderBy(issue => issue.RankOrder)
            .ThenBy(issue => issue.IssueNumber)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Issue>> GetByEpicAsync(Guid epicId, CancellationToken cancellationToken = default) =>
        await _context.Issues
            .Where(issue => issue.EpicId == epicId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Issue>> GetBySprintAsync(Guid sprintId, CancellationToken cancellationToken = default) =>
        await _context.Issues
            .AsNoTracking()
            .Where(issue => issue.SprintId == sprintId && !issue.IsDeleted)
            .OrderBy(issue => issue.RankOrder)
            .ThenBy(issue => issue.IssueNumber)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Issue>> GetOpenByAssigneeAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _context.Issues
            .AsNoTracking()
            .Where(issue => issue.AssigneeId == userId && !issue.IsDeleted && issue.ResolvedAt == null)
            .OrderBy(issue => issue.DueDate == null)
            .ThenBy(issue => issue.DueDate)
            .ThenBy(issue => issue.RankOrder)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<IssueStatusHistory>> GetStatusHistoryAsync(Guid issueId, CancellationToken cancellationToken = default) =>
        await _context.IssueStatusHistories
            .AsNoTracking()
            .Where(history => history.IssueId == issueId)
            .OrderBy(history => history.ChangedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<IssueAssignmentHistory>> GetAssignmentHistoryAsync(Guid issueId, CancellationToken cancellationToken = default) =>
        await _context.IssueAssignmentHistories
            .AsNoTracking()
            .Where(history => history.IssueId == issueId)
            .OrderBy(history => history.AssignedAt)
            .ToListAsync(cancellationToken);

    public async Task<IssuePage> ListAsync(IssueListFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var page = Math.Max(filter.Page, 1);
        var pageSize = Math.Clamp(filter.PageSize, 1, IssueListFilter.MaxPageSize);

        var query = _context.Issues
            .AsNoTracking()
            .Where(issue => issue.ProjectId == filter.ProjectId && !issue.IsDeleted);

        if (filter.BacklogOnly)
        {
            query = query.Where(issue => issue.SprintId == null);
        }
        else if (filter.SprintId.HasValue)
        {
            query = query.Where(issue => issue.SprintId == filter.SprintId);
        }

        if (filter.StatusId.HasValue)
        {
            query = query.Where(issue => issue.StatusId == filter.StatusId);
        }

        if (filter.AssigneeId.HasValue)
        {
            query = query.Where(issue => issue.AssigneeId == filter.AssigneeId);
        }

        if (filter.IssueTypeId.HasValue)
        {
            query = query.Where(issue => issue.IssueTypeId == filter.IssueTypeId);
        }

        if (filter.ParentId.HasValue)
        {
            query = query.Where(issue => issue.ParentId == filter.ParentId);
        }

        if (filter.EpicId.HasValue)
        {
            query = query.Where(issue => issue.EpicId == filter.EpicId);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var text = filter.SearchText.Trim();
            var hasNumber = int.TryParse(text, out var number);
            query = query.Where(issue => issue.Title.Contains(text) || (hasNumber && issue.IssueNumber == number));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(issue => issue.RankOrder)
            .ThenBy(issue => issue.IssueNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new IssuePage(items, totalCount);
    }

    public async Task<IReadOnlyList<Issue>> GetRankedAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        await _context.Issues
            .Where(issue => issue.ProjectId == projectId && !issue.IsDeleted)
            .OrderBy(issue => issue.RankOrder)
            .ThenBy(issue => issue.IssueNumber)
            .ToListAsync(cancellationToken);

    public async Task<decimal?> GetMaxRankAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        await _context.Issues
            .Where(issue => issue.ProjectId == projectId && !issue.IsDeleted)
            .MaxAsync(issue => (decimal?)issue.RankOrder, cancellationToken);

    public async Task AddAsync(Issue issue, CancellationToken cancellationToken = default) =>
        await _context.Issues.AddAsync(issue, cancellationToken);

    public async Task AddActivityAsync(ActivityLog activity, CancellationToken cancellationToken = default) =>
        await _context.ActivityLogs.AddAsync(activity, cancellationToken);

    public async Task<IssueStatusHistory?> GetLastStatusChangeAsync(Guid issueId, CancellationToken cancellationToken = default)
    {
        // Ưu tiên bản ghi chưa lưu trong cùng unit of work (ví dụ lúc vừa tạo Issue rồi đổi trạng thái ngay).
        var pending = _context.IssueStatusHistories.Local
            .Where(history => history.IssueId == issueId)
            .MaxBy(history => history.ChangedAt);
        return pending ?? await _context.IssueStatusHistories
            .AsNoTracking()
            .Where(history => history.IssueId == issueId)
            .OrderByDescending(history => history.ChangedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddStatusHistoryAsync(IssueStatusHistory history, CancellationToken cancellationToken = default) =>
        await _context.IssueStatusHistories.AddAsync(history, cancellationToken);

    public async Task AddAssignmentHistoryAsync(IssueAssignmentHistory history, CancellationToken cancellationToken = default) =>
        await _context.IssueAssignmentHistories.AddAsync(history, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);

    public async Task<Issue?> LockForUpdateAsync(Guid issueId, CancellationToken cancellationToken = default)
    {
        var rows = await _context.Issues
            .FromSqlInterpolated($"SELECT * FROM `Issue` WHERE `Id` = {issueId} AND `IsDeleted` = 0 FOR UPDATE")
            .ToListAsync(cancellationToken);
        return rows.SingleOrDefault();
    }

    public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        return _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var result = await work(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }
}
