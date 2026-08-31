namespace ProjectMgmt.Modules.Planning.SprintBacklog.Domain.Entities;

public sealed class Sprint
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Goal { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateTime? ActualStartAt { get; set; }
    public DateTime? ActualCompleteAt { get; set; }
    public string Status { get; set; } = "Planned";
    public int OrderIndex { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? ActiveGuard { get; set; }
}

public sealed class SprintSnapshot
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

public sealed class SprintMemberCapacity
{
    public Guid Id { get; set; }
    public Guid SprintId { get; set; }
    public Guid UserId { get; set; }
    public decimal? CapacityPoints { get; set; }
    public decimal? AvailableHours { get; set; }
    public string? Note { get; set; }
}
