namespace ProjectMgmt.Core.Security;

/// <summary>
/// Exception thrown when a tenant or project boundary invariant is violated.
/// Maps to HTTP 403 Forbidden in web layers without exposing sensitive internal data.
/// </summary>
public class ProjectScopeException : Exception
{
    public const string DefaultErrorCode = "PROJECT_SCOPE_VIOLATION";

    public string ErrorCode { get; }
    public Guid? CurrentOrgId { get; }
    public Guid? CurrentProjectId { get; }
    public Guid? CurrentUserId { get; }
    public Guid? TargetOrgId { get; }
    public Guid? TargetProjectId { get; }

    public ProjectScopeException(
        string message,
        string errorCode = DefaultErrorCode,
        Guid? currentOrgId = null,
        Guid? currentProjectId = null,
        Guid? currentUserId = null,
        Guid? targetOrgId = null,
        Guid? targetProjectId = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        CurrentOrgId = currentOrgId;
        CurrentProjectId = currentProjectId;
        CurrentUserId = currentUserId;
        TargetOrgId = targetOrgId;
        TargetProjectId = targetProjectId;
    }
}
