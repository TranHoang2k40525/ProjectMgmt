using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Infrastructure;
using IdentityExperience.Application.IServices;
using Microsoft.EntityFrameworkCore;
using Planning.Infrastructure;

namespace ProjectMgmt.Solution.Services;

public class UserWorkReader : IUserWorkReader
{
    private readonly DeliveryIntelligenceDbContext _deliveryContext;
    private readonly PlanningDbContext _planningContext;
    private readonly TimeProvider _timeProvider;

    public UserWorkReader(
        DeliveryIntelligenceDbContext deliveryContext,
        PlanningDbContext planningContext,
        TimeProvider timeProvider)
    {
        _deliveryContext = deliveryContext;
        _planningContext = planningContext;
        _timeProvider = timeProvider;
    }

    public async Task<UserWorkOverview> GetOverviewAsync(Guid userId)
    {
        var doneStatusIds = await _planningContext.WorkflowStatuses
            .AsNoTracking()
            .Where(status => status.Category == "Done")
            .Select(status => status.Id)
            .ToListAsync();
        var assignedQuery = _deliveryContext.Issues
            .AsNoTracking()
            .Where(issue => issue.AssigneeId == userId && !issue.IsDeleted);
        var activeQuery = assignedQuery.Where(issue => !doneStatusIds.Contains(issue.StatusId));
        var assigned = await activeQuery
            .OrderBy(issue => issue.DueDate == null)
            .ThenBy(issue => issue.DueDate)
            .ThenByDescending(issue => issue.UpdatedAt ?? issue.CreatedAt)
            .Take(10)
            .ToListAsync();
        var recent = await _deliveryContext.Issues
            .AsNoTracking()
            .Where(issue => issue.ReporterId == userId && !issue.IsDeleted)
            .OrderByDescending(issue => issue.UpdatedAt ?? issue.CreatedAt)
            .Take(10)
            .ToListAsync();

        var issueStatusIds = assigned.Concat(recent)
            .Select(issue => issue.StatusId)
            .Distinct()
            .ToList();
        var statusNames = await _planningContext.WorkflowStatuses
            .AsNoTracking()
            .Where(status => issueStatusIds.Contains(status.Id))
            .ToDictionaryAsync(status => status.Id, status => status.Name);
        var projectIds = assigned.Concat(recent)
            .Select(issue => issue.ProjectId)
            .Distinct()
            .ToList();
        var projectKeys = await _planningContext.Projects
            .AsNoTracking()
            .Where(project => projectIds.Contains(project.Id))
            .ToDictionaryAsync(project => project.Id, project => project.ProjectKey);

        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var dueSoonEnd = today.AddDays(3);
        var activeCount = await activeQuery.CountAsync();
        var overdueCount = await activeQuery.CountAsync(issue =>
            issue.DueDate.HasValue && issue.DueDate.Value < today);
        var dueSoonCount = await activeQuery.CountAsync(issue =>
            issue.DueDate.HasValue
            && issue.DueDate.Value >= today
            && issue.DueDate.Value <= dueSoonEnd);

        return new UserWorkOverview
        {
            AssignedIssues = assigned.Select(issue => Map(issue, projectKeys, statusNames)).ToList(),
            RecentIssues = recent.Select(issue => Map(issue, projectKeys, statusNames)).ToList(),
            ActiveCount = activeCount,
            OverdueCount = overdueCount,
            DueSoonCount = dueSoonCount
        };
    }

    private static UserWorkItem Map(
        Issue issue,
        Dictionary<Guid, string> projectKeys,
        Dictionary<Guid, string> statusNames)
    {
        projectKeys.TryGetValue(issue.ProjectId, out var projectKey);
        statusNames.TryGetValue(issue.StatusId, out var statusName);
        return new UserWorkItem
        {
            IssueId = issue.Id,
            ProjectId = issue.ProjectId,
            IssueKey = $"{projectKey ?? "ISSUE"}-{issue.IssueNumber}",
            Title = issue.Title,
            StatusName = statusName,
            DueDate = issue.DueDate
        };
    }
}
