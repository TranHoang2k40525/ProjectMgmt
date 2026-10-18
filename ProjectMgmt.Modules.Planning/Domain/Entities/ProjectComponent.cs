namespace Planning.Domain.Entities;

public class ProjectComponent
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? LeadUserId { get; set; }
    public Guid? DefaultSkillId { get; set; }
    public DateTime CreatedAt { get; set; }
}
