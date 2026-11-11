namespace ProjectMgmt.Core.Security;

/// <summary>
/// Standard immutable implementation of IProjectScopeContext.
/// </summary>
public class ProjectScopeContext : IProjectScopeContext
{
    public Guid OrganizationId { get; }
    public Guid ProjectId { get; }
    public Guid UserId { get; }

    public ProjectScopeContext(Guid organizationId, Guid projectId, Guid userId)
    {
        OrganizationId = organizationId;
        ProjectId = projectId;
        UserId = userId;
    }

    /// <summary>
    /// Creates a new immutable ProjectScopeContext instance.
    /// </summary>
    public static ProjectScopeContext Create(Guid organizationId, Guid projectId, Guid userId) =>
        new(organizationId, projectId, userId);

    /// <summary>
    /// Represents an empty unauthenticated scope context.
    /// </summary>
    public static ProjectScopeContext Empty =>
        new(Guid.Empty, Guid.Empty, Guid.Empty);
}
