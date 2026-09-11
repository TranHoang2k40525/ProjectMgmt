namespace Planning.Domain.Entities;

public class UserWorkloadSnapshot
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? SprintId { get; set; }
    public DateOnly SnapshotDate { get; set; }
    public int OpenIssueCount { get; set; }
    public int InProgressCount { get; set; }
    public decimal OpenPoints { get; set; }
    public decimal InProgressPoints { get; set; }
    public int OverdueCount { get; set; }
    public decimal? CapacityPoints { get; set; }
    public decimal? UtilizationRatio { get; set; }
    public DateTime CreatedAt { get; set; }
}
