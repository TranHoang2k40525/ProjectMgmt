namespace ProjectMgmt.Tests.McpServers;

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectMgmt.Core.Security;
using ProjectMgmt.Solution.McpServers;
using Xunit;

public class ChallengerM2AdversarialTests
{
    private readonly McpToolRegistry _registry;
    private readonly Guid _tenantA_Org = Guid.NewGuid();
    private readonly Guid _tenantA_Project = Guid.NewGuid();
    private readonly Guid _userA = Guid.NewGuid();

    public ChallengerM2AdversarialTests()
    {
        var specHub = new SpecHubTools(null!, null!, NullLogger<SpecHubTools>.Instance);
        var backlog = new BacklogTools(null!, null!);
        var sprint = new SprintTools(null!, null!);
        var team = new TeamTools(null!, null!);
        var planning = new PlanningTools(null!, null!);

        _registry = new McpToolRegistry(
            specHub,
            backlog,
            sprint,
            team,
            planning,
            NullLogger<McpToolRegistry>.Instance);
    }

    [Theory]
    [InlineData("spec_hub_list_documents", "{\"projectId\": 12345}")]
    [InlineData("spec_hub_list_documents", "{\"projectId\": true}")]
    [InlineData("spec_hub_list_documents", "{\"projectId\": {\"nested\": \"value\"}}")]
    [InlineData("spec_hub_list_documents", "{\"projectId\": [\"not_a_string\"]}")]
    [InlineData("spec_hub_read_document", "{\"documentId\": 99999}")]
    [InlineData("spec_hub_read_document", "{\"documentId\": {\"id\": \"val\"}}")]
    [InlineData("spec_hub_search", "{\"projectId\": \"00000000-0000-0000-0000-000000000000\", \"keyword\": 12345}")]
    [InlineData("spec_hub_search", "{\"projectId\": \"00000000-0000-0000-0000-000000000000\", \"keyword\": {\"nested\": true}}")]
    [InlineData("ai_sprint_plan_propose", "{\"projectId\": \"00000000-0000-0000-0000-000000000000\", \"meetingNotes\": 555}")]
    [InlineData("ai_sprint_plan_propose", "{\"projectId\": \"00000000-0000-0000-0000-000000000000\", \"meetingNotes\": {\"agenda\": \"test\"}}")]
    [InlineData("ai_sprint_plan_propose", "{\"projectId\": \"00000000-0000-0000-0000-000000000000\", \"meetingNotes\": \"Valid notes\", \"targetStoryIds\": [{\"bad\": 1}]}")]
    public async Task ExecuteToolAsync_NonStringOrNestedArguments_HandlesGracefullyWithoutThrowing(string toolName, string malformedPayload)
    {
        var scope = new ProjectScopeContext(_tenantA_Org, _tenantA_Project, _userA);

        var ex = await Record.ExceptionAsync(() => _registry.ExecuteToolAsync(toolName, malformedPayload, scope));
        Assert.Null(ex);

        var result = await _registry.ExecuteToolAsync(toolName, malformedPayload, scope);
        Assert.NotNull(result);
        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task ExecuteToolAsync_EmptyScopeContext_BypassesScopeValidation_ReturnsInvalidProjectIdInsteadOfProjectScopeException()
    {
        // When scope is invalid/empty, tools return INVALID_PROJECT_ID instead of throwing ProjectScopeException
        // because ProjectScopeGuard is only called AFTER database queries or not at all on empty GUIDs.
        var emptyScope = ProjectScopeContext.Empty;

        var result = await _registry.ExecuteToolAsync("spec_hub_list_documents", "{}", emptyScope);

        // The method didn't throw ProjectScopeException: it returned an error JSON
        Assert.Contains("INVALID_PROJECT_ID", result);
    }

    [Fact]
    public async Task McpToolRegistry_UnknownTool_PreservesUtf8VietnameseEncoding()
    {
        var scope = new ProjectScopeContext(_tenantA_Org, _tenantA_Project, _userA);
        var result = await _registry.ExecuteToolAsync("non_existent_tool", "{}", scope);

        Assert.Contains("không tồn tại", result);

        using var jsonDoc = JsonDocument.Parse(result);
        Assert.Equal("TOOL_NOT_FOUND", jsonDoc.RootElement.GetProperty("error").GetString());
        Assert.Equal("Công cụ 'non_existent_tool' không tồn tại trong hệ thống.", jsonDoc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public void ProjectScopeGuard_EmptyGuidTargetOrg_ThrowsProjectScopeException()
    {
        var scope = new ProjectScopeContext(_tenantA_Org, _tenantA_Project, _userA);

        var ex = Assert.Throws<ProjectScopeException>(() =>
            ProjectScopeGuard.Validate(scope, Guid.Empty, _tenantA_Project));

        Assert.Equal("PROJECT_SCOPE_VIOLATION", ex.ErrorCode);
    }

    [Fact]
    public void ProjectScopeGuard_EmptyGuidTargetProject_ThrowsProjectScopeException()
    {
        var scope = new ProjectScopeContext(_tenantA_Org, _tenantA_Project, _userA);

        var ex = Assert.Throws<ProjectScopeException>(() =>
            ProjectScopeGuard.Validate(scope, _tenantA_Org, Guid.Empty));

        Assert.Equal("PROJECT_SCOPE_VIOLATION", ex.ErrorCode);
    }

    [Fact]
    public void ProjectScopeGuard_ValidateProject_CrossProjectMismatch_ThrowsProjectScopeException()
    {
        var scope = new ProjectScopeContext(_tenantA_Org, _tenantA_Project, _userA);
        var foreignProject = Guid.NewGuid();

        var ex = Assert.Throws<ProjectScopeException>(() =>
            ProjectScopeGuard.ValidateProject(scope, foreignProject));

        Assert.Equal("PROJECT_SCOPE_VIOLATION", ex.ErrorCode);
        Assert.Equal(_tenantA_Project, ex.CurrentProjectId);
        Assert.Equal(foreignProject, ex.TargetProjectId);
    }

    [Fact]
    public void ProjectScopeGuard_ValidateOrganization_CrossOrgMismatch_ThrowsProjectScopeException()
    {
        var scope = new ProjectScopeContext(_tenantA_Org, _tenantA_Project, _userA);
        var foreignOrg = Guid.NewGuid();

        var ex = Assert.Throws<ProjectScopeException>(() =>
            ProjectScopeGuard.ValidateOrganization(scope, foreignOrg));

        Assert.Equal("PROJECT_SCOPE_VIOLATION", ex.ErrorCode);
        Assert.Equal(_tenantA_Org, ex.CurrentOrgId);
        Assert.Equal(foreignOrg, ex.TargetOrgId);
    }

    [Fact]
    public void ProjectScopeGuard_EnsureValidScope_EmptyOrgOrProjectOrUser_ThrowsProjectScopeException()
    {
        Assert.Throws<ProjectScopeException>(() =>
            ProjectScopeGuard.EnsureValidScope(new ProjectScopeContext(Guid.Empty, _tenantA_Project, _userA)));

        Assert.Throws<ProjectScopeException>(() =>
            ProjectScopeGuard.EnsureValidScope(new ProjectScopeContext(_tenantA_Org, Guid.Empty, _userA)));

        Assert.Throws<ProjectScopeException>(() =>
            ProjectScopeGuard.EnsureValidScope(new ProjectScopeContext(_tenantA_Org, _tenantA_Project, Guid.Empty)));
    }
}
