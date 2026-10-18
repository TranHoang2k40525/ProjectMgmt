namespace IdentityExperience.Domain.Entities;

public class Permission
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Grouping { get; set; }
}
