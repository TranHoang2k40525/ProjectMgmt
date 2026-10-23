using IdentityExperience.Domain.IRepositories;
using ProjectMgmt.IdentityAccess.Contracts;

namespace IdentityExperience.Application.Services;

public class PermissionEvaluator : IPermissionEvaluator
{
    private readonly IRbacRepository _rbacRepository;

    public PermissionEvaluator(IRbacRepository rbacRepository)
    {
        _rbacRepository = rbacRepository;
    }

    public Task<bool> HasPermissionAsync(
        Guid userId,
        string permissionCode,
        string scopeType,
        Guid? scopeId,
        Guid? organizationId = null)
    {
        if (userId == Guid.Empty
            || string.IsNullOrWhiteSpace(permissionCode)
            || string.IsNullOrWhiteSpace(scopeType))
        {
            return Task.FromResult(false);
        }

        return _rbacRepository.HasPermissionAsync(
            userId,
            permissionCode.Trim(),
            scopeType.Trim(),
            scopeId,
            organizationId);
    }

    public Task<bool> IsProjectMemberAsync(Guid userId, Guid projectId)
    {
        return userId == Guid.Empty || projectId == Guid.Empty
            ? Task.FromResult(false)
            : _rbacRepository.IsProjectMemberAsync(userId, projectId);
    }
}
