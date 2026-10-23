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
        if (!scopeId.HasValue)
        {
            return false;
        }

        var result = await _rbacRepository.GrantRoleForProvisioningAsync(
            userId,
            roleName,
            scopeType,
            scopeId.Value,
            grantedBy,
            createdAtUtc);
        return result.Status is RbacWriteStatus.Created or RbacWriteStatus.DuplicateRole;
    }
}
