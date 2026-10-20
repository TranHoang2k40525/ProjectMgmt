namespace IdentityExperience.Application.Dto;

public class ForYouDto : Result
{
    public Guid? IssueId { get; set; }
    public Guid? ProjectId { get; set; }
    public string? IssueKey { get; set; }
    public string? Title { get; set; }
    public string? StatusName { get; set; }
    public DateOnly? DueDate { get; set; }
    public List<ForYouDto>? AssignedIssues { get; set; }
    public List<ForYouDto>? RecentIssues { get; set; }
    public List<string>? AttentionFocus { get; set; }
}
