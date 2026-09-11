namespace IdentityExperience.Domain.Entities;

public class UserRole
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public string ScopeType { get; set; } = string.Empty;
    public Guid? ScopeId { get; set; }
    public Guid? GrantedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? ScopeKey { get; private set; }
}
