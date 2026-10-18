namespace DeliveryIntelligence.Domain.Entities;

public class IssueStatusHistory
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public Guid? FromStatusId { get; set; }
    public Guid ToStatusId { get; set; }
    public string? FromCategory { get; set; }
    public string ToCategory { get; set; } = string.Empty;
    public Guid? ChangedBy { get; set; }
    public long? DurationSeconds { get; set; }
    public DateTime ChangedAt { get; set; }
}
