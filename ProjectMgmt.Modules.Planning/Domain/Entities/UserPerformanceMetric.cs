namespace Planning.Domain.Entities;

public class UserPerformanceMetric
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? SprintId { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public int AssignedIssueCount { get; set; }
    public int CompletedIssueCount { get; set; }
    public decimal CommittedPoints { get; set; }
    public decimal CompletedPoints { get; set; }
    public decimal? AvgCycleTimeHours { get; set; }
    public decimal? MedianCycleTimeHours { get; set; }
    public decimal? OnTimeRatio { get; set; }
    public int ReopenedCount { get; set; }
    public decimal? EstimateAccuracyRatio { get; set; }
    public DateTime CalculatedAt { get; set; }
}
