namespace DeliveryIntelligence.Domain.Entities;

public class SprintVelocity
{
    public Guid ProjectId { get; set; }
    public Guid SprintId { get; set; }
    public string SprintName { get; set; } = string.Empty;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal CompletedPoints { get; set; }
    public decimal CommittedPoints { get; set; }
    public long IssueCount { get; set; }
}
