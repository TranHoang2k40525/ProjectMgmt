namespace ProjectMgmt.Tests.McpServers;

using ProjectMgmt.Core.Security;
using Xunit;

public class ProjectScopeGuardTests
{
    private readonly Guid _orgA = Guid.NewGuid();
    private readonly Guid _orgB = Guid.NewGuid();
    private readonly Guid _projectA = Guid.NewGuid();
    private readonly Guid _projectB = Guid.NewGuid();
    private readonly Guid _user = Guid.NewGuid();

    [Fact]
    public void ToolInvocation_SameTenantAndProject_DoesNotThrow()
    {
        var scope = new ProjectScopeContext(_orgA, _projectA, _user);

        var exception = Record.Exception(() => ProjectScopeGuard.Validate(scope, _orgA, _projectA));

        Assert.Null(exception);
    }

    [Fact]
    public void ToolInvocation_CrossTenantOrganizationMismatch_ThrowsProjectScopeException()
    {
        var scope = new ProjectScopeContext(_orgA, _projectA, _user);

        var ex = Assert.Throws<ProjectScopeException>(() =>
            ProjectScopeGuard.Validate(scope, _orgB, _projectA));

        Assert.Equal("PROJECT_SCOPE_VIOLATION", ex.ErrorCode);
        Assert.Equal(_orgA, ex.CurrentOrgId);
        Assert.Equal(_orgB, ex.TargetOrgId);
        Assert.Contains("Tổ chức không khớp", ex.Message);
    }

    [Fact]
    public void ToolInvocation_CrossProjectMismatch_ThrowsProjectScopeException()
    {
        var scope = new ProjectScopeContext(_orgA, _projectA, _user);

        var ex = Assert.Throws<ProjectScopeException>(() =>
            ProjectScopeGuard.Validate(scope, _orgA, _projectB));

        Assert.Equal("PROJECT_SCOPE_VIOLATION", ex.ErrorCode);
        Assert.Equal(_projectA, ex.CurrentProjectId);
        Assert.Equal(_projectB, ex.TargetProjectId);
        Assert.Contains("Dự án không khớp", ex.Message);
    }

    [Fact]
    public void ToolInvocation_CrossTenantAndCrossProjectMismatch_ThrowsProjectScopeException()
    {
        var scope = new ProjectScopeContext(_orgA, _projectA, _user);

        var ex = Assert.Throws<ProjectScopeException>(() =>
            ProjectScopeGuard.Validate(scope, _orgB, _projectB));

        Assert.Equal("PROJECT_SCOPE_VIOLATION", ex.ErrorCode);
    }

    [Fact]
    public void ToolInvocation_EmptyScopeContext_ThrowsProjectScopeException()
    {
        var emptyScope = ProjectScopeContext.Empty;

        var ex = Assert.Throws<ProjectScopeException>(() =>
            ProjectScopeGuard.Validate(emptyScope, _orgA, _projectA));

        Assert.Equal("PROJECT_SCOPE_VIOLATION", ex.ErrorCode);
        Assert.Contains("không hợp lệ", ex.Message);
    }

    [Fact]
    public void ToolInvocation_EmptyTargetOrgOrProject_ThrowsProjectScopeException()
    {
        var scope = new ProjectScopeContext(_orgA, _projectA, _user);

        Assert.Throws<ProjectScopeException>(() =>
            ProjectScopeGuard.Validate(scope, Guid.Empty, _projectA));

        Assert.Throws<ProjectScopeException>(() =>
            ProjectScopeGuard.Validate(scope, _orgA, Guid.Empty));
    }
}
