using Microsoft.EntityFrameworkCore;
using Planning.Infrastructure;
using ProjectMgmt.IdentityAccess.Contracts;

namespace ProjectMgmt.Solution.Security;

public sealed class ProjectAccess(PlanningDbContext planning, IPermissionEvaluator permissions, ICurrentUserContext currentUser)
{
    public Guid UserId => currentUser.UserId ?? Guid.Empty;

    public async Task<bool> AllowsAsync(Guid projectId, string permission = "project.read")
    {
        if (UserId == Guid.Empty) return false;
        var project = await planning.Projects.AsNoTracking().SingleOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted);
        if (project is null) return false;
        if (permission == "project.read" && await permissions.IsProjectMemberAsync(UserId, projectId)) return true;
        return await permissions.HasPermissionAsync(UserId, permission, "Project", projectId, project.OrgId);
    }
}
