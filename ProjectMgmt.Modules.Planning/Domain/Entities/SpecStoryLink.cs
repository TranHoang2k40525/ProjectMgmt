namespace Planning.Domain.Entities;

public class SpecStoryLink
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Guid IssueId { get; set; }
    public string? Excerpt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
