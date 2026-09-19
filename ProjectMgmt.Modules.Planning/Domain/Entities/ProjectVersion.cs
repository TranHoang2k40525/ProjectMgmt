namespace Planning.Domain.Entities;

public class ProjectVersion
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public bool IsReleased { get; set; }
    public DateTime CreatedAt { get; set; }
}
