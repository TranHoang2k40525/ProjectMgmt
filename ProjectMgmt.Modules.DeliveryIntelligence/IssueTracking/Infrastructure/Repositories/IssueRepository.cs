using Microsoft.EntityFrameworkCore;
using ProjectMgmt.Modules.DeliveryIntelligence.Infrastructure.Persistence;
using ProjectMgmt.Modules.DeliveryIntelligence.IssueTracking.Domain.Entities;
using ProjectMgmt.Modules.DeliveryIntelligence.IssueTracking.Domain.IRepositories;

namespace ProjectMgmt.Modules.DeliveryIntelligence.IssueTracking.Infrastructure.Repositories;

internal class IssueRepository : IIssueRepository
{
    private readonly DeliveryIntelligenceAppDbContext _dbContext;

    public IssueRepository(DeliveryIntelligenceAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    public Task<Issue?> GetByIdAsync(Guid issueId, CancellationToken cancellationToken = default) =>
        _dbContext.Issues.SingleOrDefaultAsync(x => x.Id == issueId && !x.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<Issue>> GetBySprintAsync(
        Guid sprintId,
        int skip,
        int take,
        CancellationToken cancellationToken = default) =>
        await _dbContext.Issues.AsNoTracking()
            .Where(x => x.SprintId == sprintId && !x.IsDeleted)
            .OrderBy(x => x.RankOrder)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<int> CountBySprintAsync(Guid sprintId, CancellationToken cancellationToken = default) =>
        _dbContext.Issues.CountAsync(x => x.SprintId == sprintId && !x.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<Issue>> GetOpenByAssigneeAsync(
        Guid assigneeId,
        int skip,
        int take,
        CancellationToken cancellationToken = default) =>
        await _dbContext.Issues.AsNoTracking()
            .Where(x => x.AssigneeId == assigneeId && x.ResolvedAt == null && !x.IsDeleted)
            .OrderBy(x => x.DueDate)
            .ThenBy(x => x.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<int> CountOpenByAssigneeAsync(Guid assigneeId, CancellationToken cancellationToken = default) =>
        _dbContext.Issues.CountAsync(
            x => x.AssigneeId == assigneeId && x.ResolvedAt == null && !x.IsDeleted,
            cancellationToken);

    public async Task<IReadOnlyList<Issue>> GetByIdsAsync(
        IReadOnlyCollection<Guid> issueIds,
        CancellationToken cancellationToken = default) =>
        await _dbContext.Issues
            .Where(x => issueIds.Contains(x.Id) && !x.IsDeleted)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<IssueStatusHistory>> GetStatusHistoryAsync(
        Guid issueId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.IssueStatusHistory.AsNoTracking()
            .Where(x => x.IssueId == issueId)
            .OrderBy(x => x.ChangedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<IssueAssignmentHistory>> GetAssignmentHistoryAsync(
        Guid issueId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.IssueAssignmentHistory.AsNoTracking()
            .Where(x => x.IssueId == issueId)
            .OrderBy(x => x.AssignedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<IssueRequiredSkill>> GetRequiredSkillsAsync(
        IReadOnlyCollection<Guid> issueIds,
        CancellationToken cancellationToken = default) =>
        await _dbContext.IssueRequiredSkills.AsNoTracking()
            .Where(x => issueIds.Contains(x.IssueId))
            .OrderBy(x => x.IssueId)
            .ThenByDescending(x => x.Weight)
            .ToListAsync(cancellationToken);

    public Task<int> CountByStatusAsync(
        Guid projectId,
        Guid statusId,
        CancellationToken cancellationToken = default) =>
        _dbContext.Issues.CountAsync(
            x => x.ProjectId == projectId && x.StatusId == statusId && !x.IsDeleted,
            cancellationToken);

    public async Task<decimal> GetNextRankAsync(
        Guid projectId,
        Guid? sprintId,
        CancellationToken cancellationToken = default)
    {
        var maximum = await _dbContext.Issues.AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.SprintId == sprintId && !x.IsDeleted)
            .Select(x => (decimal?)x.RankOrder)
            .MaxAsync(cancellationToken);

        return (maximum ?? 0m) + 1000m;
    }

    public async Task AddAsync(Issue issue, CancellationToken cancellationToken = default) =>
        await _dbContext.Issues.AddAsync(issue, cancellationToken);

    public async Task AddStatusHistoryAsync(
        IssueStatusHistory history,
        CancellationToken cancellationToken = default) =>
        await _dbContext.IssueStatusHistory.AddAsync(history, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.SaveChangesAsync(cancellationToken);
}
