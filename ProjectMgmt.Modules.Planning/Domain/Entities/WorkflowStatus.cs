namespace Planning.Domain.Entities;

public class WorkflowStatus
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ColorHex { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public bool IsInitial { get; set; }
    public bool IsDone { get; set; }
    public bool IsActiveWork { get; set; }
    public bool IsCanceled { get; set; }
}
