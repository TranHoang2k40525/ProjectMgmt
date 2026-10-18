namespace DeliveryIntelligence.Domain.Entities;

public class IssueWatcher
{
    public Guid IssueId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
