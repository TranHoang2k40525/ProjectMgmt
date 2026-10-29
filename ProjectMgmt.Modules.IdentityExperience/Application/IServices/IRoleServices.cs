using IdentityExperience.Application.Dto;

namespace IdentityExperience.Application.IServices;

public interface IRoleServices
{
    Task<RoleDto> GetRolesAsync(string? scope);
    Task<RoleDto> GetPermissionsAsync();
    Task<RoleDto> CreateRoleAsync(RoleDto request);
    Task<RoleDto> UpdateRolePermissionsAsync(Guid roleId, List<Guid>? permissionIds);
    Task<RoleDto> GetUserRolesAsync(Guid userId, string? scopeType, Guid? scopeId);
    Task<RoleDto> AssignUserRoleAsync(Guid actorUserId, Guid userId, RoleDto request);
    Task<RoleDto> RemoveUserRoleAsync(Guid actorUserId, Guid userId, Guid userRoleId);
    Task<RoleDto> GetProjectMembersAsync(Guid projectId);
    Task<RoleDto> AddProjectMemberAsync(
        Guid actorUserId,
        Guid projectId,
        Guid? userId,
        Guid? roleId);
    Task<RoleDto> ChangeProjectMemberRoleAsync(
        Guid actorUserId,
        Guid projectId,
        Guid userId,
        Guid? newRoleId);
    Task<RoleDto> RemoveProjectMemberAsync(
        Guid actorUserId,
        Guid projectId,
        Guid userId);
    Task<RoleDto> InviteProjectMemberByEmailAsync(
        Guid actorUserId,
        Guid projectId,
        string email,
        string roleName);
    Task<RoleDto> BatchInviteProjectMembersAsync(
        Guid actorUserId,
        Guid projectId,
        string emails,
        string? names,
        string roleName);
    Task<RoleDto> RespondProjectInvitationAsync(
        Guid actorUserId,
        Guid projectId,
        Guid notificationId,
        bool accept);
}
