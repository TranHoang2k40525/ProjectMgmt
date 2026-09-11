namespace DeliveryIntelligence.Domain.Entities;

public class ActivityLog
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? FieldName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Detail { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
