namespace DeliveryIntelligence.Domain.Entities;

public class UserActiveWorkload
{
    public Guid ProjectId { get; set; }
    public Guid? SprintId { get; set; }
    public Guid UserId { get; set; }
    public long OpenIssueCount { get; set; }
    public decimal InProgressCount { get; set; }
    public decimal OpenPoints { get; set; }
    public decimal InProgressPoints { get; set; }
    public decimal OverdueCount { get; set; }
}
