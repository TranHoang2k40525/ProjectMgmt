namespace IdentityExperience.Application.Dto;

public class RoleDto : Result
{
    public Guid? RoleId { get; set; }
    public string? Name { get; set; }
    public string? Scope { get; set; }
    public bool? IsSystem { get; set; }
    public string? Description { get; set; }
    public Guid? PermissionId { get; set; }
    public string? Code { get; set; }
    public string? Grouping { get; set; }
    public List<Guid>? PermissionIds { get; set; }
    public List<RoleDto>? Items { get; set; }
    public List<RoleDto>? Roles { get; set; }
    public List<RoleDto>? Permissions { get; set; }
    public List<RoleDto>? Members { get; set; }
    public Guid? UserId { get; set; }
    public Guid? UserRoleId { get; set; }
    public string? RoleName { get; set; }
    public string? ScopeType { get; set; }
    public Guid? ScopeId { get; set; }
    public Guid? GrantedBy { get; set; }
    public DateTime? GrantedAt { get; set; }
    public int? UpdatedPermissionCount { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }
    public Guid? NewRoleId { get; set; }
    public string? NewRoleName { get; set; }
    public bool? Removed { get; set; }
}
