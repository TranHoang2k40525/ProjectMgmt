namespace Planning.Domain.Entities;

public class IssueType
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? IconKey { get; set; }
    public string ColorHex { get; set; } = string.Empty;
    public bool IsSubtask { get; set; }
    public sbyte HierarchyLevel { get; set; }
    public int OrderIndex { get; set; }
}
