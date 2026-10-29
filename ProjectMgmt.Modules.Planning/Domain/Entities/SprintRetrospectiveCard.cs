namespace Planning.Domain.Entities;

public class SprintRetrospectiveCard
{
    public Guid Id { get; set; }
    public Guid SprintId { get; set; }
    public string ColumnType { get; set; } = "Well"; // Well, Wrong, Action
    public string Content { get; set; } = string.Empty;
    public bool IsAnonymous { get; set; }
    public Guid AuthorUserId { get; set; }
    public int VoteCount { get; set; }
    public Guid? ConvertedToIssueId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
