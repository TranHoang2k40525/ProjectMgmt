namespace Planning.Domain.Entities;

public class SprintMessage
{
    public Guid Id { get; set; }
    public Guid SprintId { get; set; }
    public Guid UserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
