using IdentityExperience.Application.Dto;

namespace IdentityExperience.Application.IServices;

public interface IForYouServices
{
    Task<ForYouDto> GetAsync(Guid userId);
}

public interface IUserWorkReader
{
    Task<UserWorkOverview> GetOverviewAsync(Guid userId);
}

public class UserWorkOverview
{
    public List<UserWorkItem> AssignedIssues { get; set; } = [];
    public List<UserWorkItem> RecentIssues { get; set; } = [];
    public int ActiveCount { get; set; }
    public int OverdueCount { get; set; }
    public int DueSoonCount { get; set; }
}

public class UserWorkItem
{
    public Guid IssueId { get; set; }
    public Guid ProjectId { get; set; }
    public string IssueKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? StatusName { get; set; }
    public DateOnly? DueDate { get; set; }
}
