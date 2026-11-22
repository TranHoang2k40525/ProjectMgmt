namespace ProjectMgmt.Core.Security;

/// <summary>
/// Enforces multitenancy and project boundary isolation rules across AI tools and application services.
/// </summary>
public static class ProjectScopeGuard
{
    /// <summary>
    /// Validates that target OrganizationId and ProjectId match the active scope context.
    /// Throws ProjectScopeException if any boundary is violated or if identifiers are invalid.
    /// </summary>
    /// <param name="currentScope">The verified security context of the caller.</param>
    /// <param name="targetOrgId">Target organization identifier.</param>
    /// <param name="targetProjectId">Target project identifier.</param>
    /// <exception cref="ArgumentNullException">Thrown if currentScope is null.</exception>
    /// <exception cref="ProjectScopeException">Thrown if any boundary check fails.</exception>
    public static void Validate(IProjectScopeContext currentScope, Guid targetOrgId, Guid targetProjectId)
    {
        ArgumentNullException.ThrowIfNull(currentScope);

        EnsureValidScope(currentScope);

        if (targetOrgId == Guid.Empty)
        {
            throw new ProjectScopeException(
                "Mã tổ chức mục tiêu không được để trống (Target OrganizationId is empty).",
                currentOrgId: currentScope.OrganizationId,
                currentProjectId: currentScope.ProjectId,
                currentUserId: currentScope.UserId,
                targetOrgId: targetOrgId,
                targetProjectId: targetProjectId);
        }

        if (targetProjectId == Guid.Empty)
        {
            throw new ProjectScopeException(
                "Mã dự án mục tiêu không được để trống (Target ProjectId is empty).",
                currentOrgId: currentScope.OrganizationId,
                currentProjectId: currentScope.ProjectId,
                currentUserId: currentScope.UserId,
                targetOrgId: targetOrgId,
                targetProjectId: targetProjectId);
        }

        if (currentScope.OrganizationId != targetOrgId)
        {
            throw new ProjectScopeException(
                $"Truy cập bị từ chối: Tổ chức không khớp với ngữ cảnh hiện tại. " +
                $"CurrentOrgId={currentScope.OrganizationId}, TargetOrgId={targetOrgId}.",
                currentOrgId: currentScope.OrganizationId,
                currentProjectId: currentScope.ProjectId,
                currentUserId: currentScope.UserId,
                targetOrgId: targetOrgId,
                targetProjectId: targetProjectId);
        }

        if (currentScope.ProjectId != targetProjectId)
        {
            throw new ProjectScopeException(
                $"Truy cập bị từ chối: Dự án không khớp với ngữ cảnh hiện tại. " +
                $"CurrentProjectId={currentScope.ProjectId}, TargetProjectId={targetProjectId}.",
                currentOrgId: currentScope.OrganizationId,
                currentProjectId: currentScope.ProjectId,
                currentUserId: currentScope.UserId,
                targetOrgId: targetOrgId,
                targetProjectId: targetProjectId);
        }
    }

    /// <summary>
    /// Validates that the target project matches the active scope project.
    /// </summary>
    public static void ValidateProject(IProjectScopeContext currentScope, Guid targetProjectId)
    {
        ArgumentNullException.ThrowIfNull(currentScope);

        if (currentScope.ProjectId == Guid.Empty || currentScope.ProjectId != targetProjectId)
        {
            throw new ProjectScopeException(
                $"Truy cập bị từ chối: Dự án mục tiêu ({targetProjectId}) không thuộc ngữ cảnh của phiên làm việc ({currentScope.ProjectId}).",
                currentOrgId: currentScope.OrganizationId,
                currentProjectId: currentScope.ProjectId,
                currentUserId: currentScope.UserId,
                targetProjectId: targetProjectId);
        }
    }

    /// <summary>
    /// Validates that the target organization matches the active scope organization.
    /// </summary>
    public static void ValidateOrganization(IProjectScopeContext currentScope, Guid targetOrgId)
    {
        ArgumentNullException.ThrowIfNull(currentScope);

        if (currentScope.OrganizationId == Guid.Empty || currentScope.OrganizationId != targetOrgId)
        {
            throw new ProjectScopeException(
                $"Truy cập bị từ chối: Tổ chức mục tiêu ({targetOrgId}) không thuộc ngữ cảnh của phiên làm việc ({currentScope.OrganizationId}).",
                currentOrgId: currentScope.OrganizationId,
                currentProjectId: currentScope.ProjectId,
                currentUserId: currentScope.UserId,
                targetOrgId: targetOrgId);
        }
    }

    /// <summary>
    /// Ensures that the scope itself contains valid, non-empty OrganizationId, ProjectId, and UserId.
    /// </summary>
    public static void EnsureValidScope(IProjectScopeContext currentScope)
    {
        ArgumentNullException.ThrowIfNull(currentScope);

        if (currentScope.OrganizationId == Guid.Empty)
        {
            throw new ProjectScopeException(
                "Ngữ cảnh bảo mật không hợp lệ: OrganizationId không được là Guid.Empty.",
                currentOrgId: currentScope.OrganizationId,
                currentProjectId: currentScope.ProjectId,
                currentUserId: currentScope.UserId);
        }

        if (currentScope.ProjectId == Guid.Empty)
        {
            throw new ProjectScopeException(
                "Ngữ cảnh bảo mật không hợp lệ: ProjectId không được là Guid.Empty.",
                currentOrgId: currentScope.OrganizationId,
                currentProjectId: currentScope.ProjectId,
                currentUserId: currentScope.UserId);
        }

        if (currentScope.UserId == Guid.Empty)
        {
            throw new ProjectScopeException(
                "Ngữ cảnh bảo mật không hợp lệ: UserId không được là Guid.Empty.",
                currentOrgId: currentScope.OrganizationId,
                currentProjectId: currentScope.ProjectId,
                currentUserId: currentScope.UserId);
        }
    }
}
