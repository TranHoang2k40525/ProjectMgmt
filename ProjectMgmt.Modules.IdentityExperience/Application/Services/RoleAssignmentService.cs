using IdentityExperience.Domain.IRepositories;
using IdentityExperience.Domain.Models;
using ProjectMgmt.IdentityAccess.Contracts;

namespace IdentityExperience.Application.Services;

public class RoleAssignmentService : IRoleAssignmentService
{
    private readonly IRbacRepository _rbacRepository;

    public RoleAssignmentService(IRbacRepository rbacRepository)
    {
        _rbacRepository = rbacRepository;
    }

    public async Task<bool> GrantRoleForProvisioningAsync(
        Guid userId,
        string roleName,
        string scopeType,
        Guid? scopeId,
        Guid grantedBy,
        DateTime createdAtUtc)
    {
        var roles = await _rbacRepository.GetRolesAsync(scopeType);
        var role = roles.FirstOrDefault(item => item.Name == roleName);
        if (role is null)
        {
            return false;
        }

        var result = await _rbacRepository.GrantRoleAsync(
            userId,
            role.Id,
            scopeType,
            scopeId,
            grantedBy,
            createdAtUtc);
        return result.Status is RbacWriteStatus.Created or RbacWriteStatus.DuplicateRole;
    }
}
