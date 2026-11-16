namespace ProjectMgmt.Core.Security;

/// <summary>
/// Immutable tenant boundary context identifying the active user, organization, and project.
/// Enforces multitenancy isolation across AI services, MCP tools, and data stores.
/// </summary>
public interface IProjectScopeContext
{
    /// <summary>
    /// Identifier of the active Organization.
    /// </summary>
    Guid OrganizationId { get; }

    /// <summary>
    /// Identifier of the active Project.
    /// </summary>
    Guid ProjectId { get; }

    /// <summary>
    /// Identifier of the authenticated User executing the request.
    /// </summary>
    Guid UserId { get; }
}
