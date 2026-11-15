using ProjectMgmt.Core.Security;
using Xunit;

namespace ProjectMgmt.Tests.Core;

public class ProjectScopeGuardTests
{
    private readonly Guid _validOrgId = Guid.NewGuid();
    private readonly Guid _validProjectId = Guid.NewGuid();
    private readonly Guid _validUserId = Guid.NewGuid();

    [Fact]
    public void Validate_MatchingScope_DoesNotThrow()
    {
        var scope = new ProjectScopeContext(_validOrgId, _validProjectId, _validUserId);

        var exception = Record.Exception(() => ProjectScopeGuard.Validate(scope, _validOrgId, _validProjectId));

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_MismatchOrganization_ThrowsProjectScopeException()
    {
        var scope = new ProjectScopeContext(_validOrgId, _validProjectId, _validUserId);
        var differentOrgId = Guid.NewGuid();

        var ex = Assert.Throws<ProjectScopeException>(() =>
            ProjectScopeGuard.Validate(scope, differentOrgId, _validProjectId));

        Assert.Equal(ProjectScopeException.DefaultErrorCode, ex.ErrorCode);
        Assert.Equal(_validOrgId, ex.CurrentOrgId);
        Assert.Equal(differentOrgId, ex.TargetOrgId);
        Assert.Contains("Tổ chức không khớp", ex.Message);
    }

    [Fact]
    public void Validate_MismatchProject_ThrowsProjectScopeException()
    {
        var scope = new ProjectScopeContext(_validOrgId, _validProjectId, _validUserId);
        var differentProjectId = Guid.NewGuid();

        var ex = Assert.Throws<ProjectScopeException>(() =>
            ProjectScopeGuard.Validate(scope, _validOrgId, differentProjectId));

        Assert.Equal(ProjectScopeException.DefaultErrorCode, ex.ErrorCode);
        Assert.Equal(_validProjectId, ex.CurrentProjectId);
        Assert.Equal(differentProjectId, ex.TargetProjectId);
        Assert.Contains("Dự án không khớp", ex.Message);
    }

    [Fact]
    public void Validate_EmptyScope_ThrowsProjectScopeException()
    {
        var emptyScope = ProjectScopeContext.Empty;

        var ex = Assert.Throws<ProjectScopeException>(() =>
            ProjectScopeGuard.Validate(emptyScope, _validOrgId, _validProjectId));

        Assert.Equal(ProjectScopeException.DefaultErrorCode, ex.ErrorCode);
        Assert.Contains("OrganizationId", ex.Message);
    }

    [Fact]
    public void Validate_NullScope_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ProjectScopeGuard.Validate(null!, _validOrgId, _validProjectId));
    }

    [Fact]
    public void Validate_EmptyTargetOrgId_ThrowsProjectScopeException()
    {
        var scope = new ProjectScopeContext(_validOrgId, _validProjectId, _validUserId);

        var ex = Assert.Throws<ProjectScopeException>(() =>
            ProjectScopeGuard.Validate(scope, Guid.Empty, _validProjectId));

        Assert.Contains("Target OrganizationId is empty", ex.Message);
    }

    [Fact]
    public void Validate_EmptyTargetProjectId_ThrowsProjectScopeException()
    {
        var scope = new ProjectScopeContext(_validOrgId, _validProjectId, _validUserId);

        var ex = Assert.Throws<ProjectScopeException>(() =>
            ProjectScopeGuard.Validate(scope, _validOrgId, Guid.Empty));

        Assert.Contains("Target ProjectId is empty", ex.Message);
    }

    [Fact]
    public void ValidateProject_Matching_DoesNotThrow()
    {
        var scope = new ProjectScopeContext(_validOrgId, _validProjectId, _validUserId);

        var exception = Record.Exception(() => ProjectScopeGuard.ValidateProject(scope, _validProjectId));

        Assert.Null(exception);
    }

    [Fact]
    public void ValidateProject_Mismatch_ThrowsProjectScopeException()
    {
        var scope = new ProjectScopeContext(_validOrgId, _validProjectId, _validUserId);
        var otherProjectId = Guid.NewGuid();

        Assert.Throws<ProjectScopeException>(() =>
            ProjectScopeGuard.ValidateProject(scope, otherProjectId));
    }

    [Fact]
    public void ValidateOrganization_Matching_DoesNotThrow()
    {
        var scope = new ProjectScopeContext(_validOrgId, _validProjectId, _validUserId);

        var exception = Record.Exception(() => ProjectScopeGuard.ValidateOrganization(scope, _validOrgId));

        Assert.Null(exception);
    }

    [Fact]
    public void ValidateOrganization_Mismatch_ThrowsProjectScopeException()
    {
        var scope = new ProjectScopeContext(_validOrgId, _validProjectId, _validUserId);
        var otherOrgId = Guid.NewGuid();

        Assert.Throws<ProjectScopeException>(() =>
            ProjectScopeGuard.ValidateOrganization(scope, otherOrgId));
    }

    [Fact]
    public void EnsureValidScope_EmptyUserId_ThrowsProjectScopeException()
    {
        var scope = new ProjectScopeContext(_validOrgId, _validProjectId, Guid.Empty);

        var ex = Assert.Throws<ProjectScopeException>(() => ProjectScopeGuard.EnsureValidScope(scope));

        Assert.Contains("UserId", ex.Message);
    }

    [Fact]
    public void EnsureValidScope_EmptyProjectId_ThrowsProjectScopeException()
    {
        var scope = new ProjectScopeContext(_validOrgId, Guid.Empty, _validUserId);

        var ex = Assert.Throws<ProjectScopeException>(() => ProjectScopeGuard.EnsureValidScope(scope));

        Assert.Contains("ProjectId", ex.Message);
    }
}
