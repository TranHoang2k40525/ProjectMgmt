namespace Planning.Domain.Entities;

public class SprintSnapshot
{
    public Guid Id { get; set; }
    public Guid SprintId { get; set; }
    public DateOnly SnapshotDate { get; set; }
    public decimal TotalPoints { get; set; }
    public decimal RemainingPoints { get; set; }
    public decimal CompletedPoints { get; set; }
    public decimal AddedPoints { get; set; }
    public int RemainingIssueCount { get; set; }
    public int TotalIssueCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
