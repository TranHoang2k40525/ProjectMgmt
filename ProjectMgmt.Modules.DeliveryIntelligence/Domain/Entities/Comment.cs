namespace DeliveryIntelligence.Domain.Entities;

public class Comment
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public Guid UserId { get; set; }
    public Guid? ParentCommentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? MentionedUserIds { get; set; }
    public bool IsEdited { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
