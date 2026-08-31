using ProjectMgmt.Modules.DeliveryIntelligence.IssueTracking.Domain.Entities;

namespace ProjectMgmt.Modules.DeliveryIntelligence.IssueTracking.Domain.IRepositories;

public interface IIssueRepository
{
    Task<Issue?> GetByIdAsync(Guid issueId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Issue>> GetBySprintAsync(Guid sprintId, int skip, int take, CancellationToken cancellationToken = default);
    Task<int> CountBySprintAsync(Guid sprintId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Issue>> GetOpenByAssigneeAsync(Guid assigneeId, int skip, int take, CancellationToken cancellationToken = default);
    Task<int> CountOpenByAssigneeAsync(Guid assigneeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Issue>> GetByIdsAsync(IReadOnlyCollection<Guid> issueIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IssueStatusHistory>> GetStatusHistoryAsync(Guid issueId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IssueAssignmentHistory>> GetAssignmentHistoryAsync(Guid issueId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IssueRequiredSkill>> GetRequiredSkillsAsync(IReadOnlyCollection<Guid> issueIds, CancellationToken cancellationToken = default);
    Task<int> CountByStatusAsync(Guid projectId, Guid statusId, CancellationToken cancellationToken = default);
    Task<decimal> GetNextRankAsync(Guid projectId, Guid? sprintId, CancellationToken cancellationToken = default);
    Task AddAsync(Issue issue, CancellationToken cancellationToken = default);
    Task AddStatusHistoryAsync(IssueStatusHistory history, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
