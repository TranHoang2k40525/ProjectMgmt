using IdentityExperience.Domain.Entities;
using IdentityExperience.Domain.Models;

namespace IdentityExperience.Domain.IRepositories;

public interface IRbacRepository
{
    Task<List<Role>> GetRolesAsync(string? scope);
    Task<List<Permission>> GetPermissionsAsync();
    Task<RbacWriteResult> CreateRoleAsync(Role role, List<Guid> permissionIds);
    Task<RbacWriteResult> UpdateRolePermissionsAsync(Guid roleId, List<Guid> permissionIds);
    Task<List<ScopedRoleDetails>> GetUserRolesAsync(Guid userId, string? scopeType, Guid? scopeId);
    Task<RbacWriteResult> GrantRoleAsync(
        Guid userId,
        Guid roleId,
        string scopeType,
        Guid? scopeId,
        Guid grantedBy,
        DateTime createdAtUtc);
    Task<RbacWriteResult> RemoveUserRoleAsync(Guid userId, Guid userRoleId);
    Task<List<ProjectMemberDetails>> GetProjectMembersAsync(Guid projectId);
    Task<RbacWriteResult> AddProjectMemberAsync(
        Guid userId,
        Guid roleId,
        Guid projectId,
        Guid grantedBy,
        DateTime createdAtUtc);
    Task<RbacWriteResult> ChangeProjectMemberRoleAsync(
        Guid userId,
        Guid newRoleId,
        Guid projectId,
        Guid grantedBy,
        DateTime createdAtUtc);
    Task<RbacWriteResult> RemoveProjectMemberAsync(Guid userId, Guid projectId);
    Task<bool> HasPermissionAsync(
        Guid userId,
        string permissionCode,
        string scopeType,
        Guid? scopeId,
        Guid? organizationId);
    Task<bool> IsProjectMemberAsync(Guid userId, Guid projectId);
}
