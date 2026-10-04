using IdentityExperience.Domain.Entities;

namespace IdentityExperience.Domain.Models;

public class ScopedRoleDetails
{
    public UserRole UserRole { get; set; } = new();
    public Role Role { get; set; } = new();
}

public class ProjectMemberDetails
{
    public User User { get; set; } = new();
    public UserProfile? Profile { get; set; }
    public List<ScopedRoleDetails> Roles { get; set; } = [];
}

public enum RbacWriteStatus
{
    Created,
    Updated,
    Removed,
    UserNotFound,
    RoleNotFound,
    PermissionNotFound,
    ScopeMismatch,
    DuplicateRole,
    MemberAlreadyExists,
    MemberNotFound,
    UserRoleNotFound,
    LastProjectManager,
    Conflict
}

public class RbacWriteResult
{
    public RbacWriteStatus Status { get; set; }
    public Role? Role { get; set; }
    public UserRole? UserRole { get; set; }
    public int UpdatedCount { get; set; }
}
