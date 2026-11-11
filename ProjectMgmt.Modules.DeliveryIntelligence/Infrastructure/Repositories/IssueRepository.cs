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

    public Task<Issue?> GetByIdAsync(Guid issueId, bool includeDeleted = false) =>
        _context.Issues.SingleOrDefaultAsync(
            issue => issue.Id == issueId && (includeDeleted || !issue.IsDeleted));

    public Task<Issue?> GetByNumberAsync(Guid projectId, int issueNumber) =>
        _context.Issues.SingleOrDefaultAsync(
            issue => issue.ProjectId == projectId && issue.IssueNumber == issueNumber && !issue.IsDeleted);

    public async Task<IReadOnlyList<Issue>> GetByIdsAsync(IEnumerable<Guid> issueIds)
    {
        var ids = issueIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await _context.Issues
            .Where(issue => ids.Contains(issue.Id) && !issue.IsDeleted)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Issue>> GetChildrenAsync(Guid parentId) =>
        await _context.Issues
            .Where(issue => issue.ParentId == parentId && !issue.IsDeleted)
            .OrderBy(issue => issue.RankOrder)
            .ThenBy(issue => issue.IssueNumber)
            .ToListAsync();

    public async Task<IReadOnlyList<Issue>> GetByEpicAsync(Guid epicId) =>
        await _context.Issues
            .Where(issue => issue.EpicId == epicId)
            .ToListAsync();

    public async Task<IReadOnlyList<Issue>> GetBySprintAsync(Guid sprintId) =>
        await _context.Issues
            .AsNoTracking()
            .Where(issue => issue.SprintId == sprintId && !issue.IsDeleted)
            .OrderBy(issue => issue.RankOrder)
            .ThenBy(issue => issue.IssueNumber)
            .ToListAsync();

    public async Task<IReadOnlyList<Issue>> GetOpenByAssigneeAsync(Guid userId) =>
        await _context.Issues
            .AsNoTracking()
            .Where(issue => issue.AssigneeId == userId && !issue.IsDeleted && issue.ResolvedAt == null)
            .OrderBy(issue => issue.DueDate == null)
            .ThenBy(issue => issue.DueDate)
            .ThenBy(issue => issue.RankOrder)
            .ToListAsync();

    public async Task<IReadOnlyList<IssueStatusHistory>> GetStatusHistoryAsync(Guid issueId) =>
        await _context.IssueStatusHistories
            .AsNoTracking()
            .Where(history => history.IssueId == issueId)
            .OrderBy(history => history.ChangedAt)
            .ToListAsync();

    public async Task<IReadOnlyList<IssueAssignmentHistory>> GetAssignmentHistoryAsync(Guid issueId) =>
        await _context.IssueAssignmentHistories
            .AsNoTracking()
            .Where(history => history.IssueId == issueId)
            .OrderBy(history => history.AssignedAt)
            .ToListAsync();

    public async Task<IssuePage> ListAsync(IssueListFilter filter)
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

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(issue => issue.RankOrder)
            .ThenBy(issue => issue.IssueNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new IssuePage(items, totalCount);
    }

    public async Task<IReadOnlyList<Issue>> GetRankedAsync(Guid projectId) =>
        await _context.Issues
            .Where(issue => issue.ProjectId == projectId && !issue.IsDeleted)
            .OrderBy(issue => issue.RankOrder)
            .ThenBy(issue => issue.IssueNumber)
            .ToListAsync();

    public async Task<decimal?> GetMaxRankAsync(Guid projectId) =>
        await _context.Issues
            .Where(issue => issue.ProjectId == projectId && !issue.IsDeleted)
            .MaxAsync(issue => (decimal?)issue.RankOrder);

    public async Task AddAsync(Issue issue) =>
        await _context.Issues.AddAsync(issue);

    public async Task AddActivityAsync(ActivityLog activity) =>
        await _context.ActivityLogs.AddAsync(activity);

    public async Task<(IReadOnlyList<ActivityLog> Items, int TotalCount)> GetActivitiesAsync(
        Guid issueId,
        int page,
        int pageSize)
    {
        var query = _context.ActivityLogs.AsNoTracking().Where(activity => activity.IssueId == issueId);
        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(activity => activity.CreatedAt)
            .ThenBy(activity => activity.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return (items, total);
    }

    public async Task<IssueStatusHistory?> GetLastStatusChangeAsync(Guid issueId)
    {
        // Ưu tiên bản ghi chưa lưu trong cùng unit of work (ví dụ lúc vừa tạo Issue rồi đổi trạng thái ngay).
        var pending = _context.IssueStatusHistories.Local
            .Where(history => history.IssueId == issueId)
            .MaxBy(history => history.ChangedAt);
        return pending ?? await _context.IssueStatusHistories
            .AsNoTracking()
            .Where(history => history.IssueId == issueId)
            .OrderByDescending(history => history.ChangedAt)
            .FirstOrDefaultAsync();
    }

    public async Task AddStatusHistoryAsync(IssueStatusHistory history) =>
        await _context.IssueStatusHistories.AddAsync(history);

    public async Task AddAssignmentHistoryAsync(IssueAssignmentHistory history) =>
        await _context.IssueAssignmentHistories.AddAsync(history);

    public Task SaveChangesAsync() =>
        _context.SaveChangesAsync();

    public async Task<Issue?> LockForUpdateAsync(Guid issueId)
    {
        // Identity map của EF sẽ trả lại instance đã track (giá trị cũ) thay vì dòng vừa khóa: gỡ nó trước khi đọc lại.
        var tracked = _context.ChangeTracker.Entries<Issue>().FirstOrDefault(entry => entry.Entity.Id == issueId);
        if (tracked is not null)
        {
            if (tracked.State != EntityState.Unchanged)
            {
                throw new InvalidOperationException("Issue đang có thay đổi chưa lưu, không thể khóa để đọc lại.");
            }

            tracked.State = EntityState.Detached;
        }

        var rows = await _context.Issues
            .FromSqlInterpolated($"SELECT * FROM `Issue` WHERE `Id` = {issueId} AND `IsDeleted` = 0 FOR UPDATE")
            .ToListAsync();
        return rows.SingleOrDefault();
    }

    public Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        var attempt = 0;
        return _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            // Chỉ xóa tracker khi execution strategy chạy lại sau lỗi tạm thời, để không làm mất thay đổi của người gọi.
            if (attempt++ > 0)
            {
                _context.ChangeTracker.Clear();
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            var result = await work();
            await transaction.CommitAsync();
            return result;
        });
    }
}
