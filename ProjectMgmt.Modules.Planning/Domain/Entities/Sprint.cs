namespace Planning.Domain.Entities;

public class Sprint
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Goal { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateTime? ActualStartAt { get; set; }
    public DateTime? ActualCompleteAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? ActiveGuard { get; private set; }
}
