namespace ProjectMgmt.Tests.McpServers;

using Microsoft.Extensions.Logging.Abstractions;
using ProjectMgmt.Core.Security;
using ProjectMgmt.Solution.McpServers;
using Xunit;

public class McpToolRegistryTests
{
    private readonly McpToolRegistry _registry;

    public McpToolRegistryTests()
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

    [Fact]
    public void GetToolDefinitions_ReturnsAllSevenToolsWithValidSchemas()
    {
        var tools = _registry.GetToolDefinitions();

        Assert.Equal(7, tools.Count);

        var toolNames = tools.Select(t => t.Name).ToHashSet();
        Assert.Contains("spec_hub_list_documents", toolNames);
        Assert.Contains("spec_hub_read_document", toolNames);
        Assert.Contains("spec_hub_search", toolNames);
        Assert.Contains("backlog_get_stories", toolNames);
        Assert.Contains("sprint_get_history_and_velocity", toolNames);
        Assert.Contains("team_get_dev_capacities", toolNames);
        Assert.Contains("ai_sprint_plan_propose", toolNames);

        foreach (var tool in tools)
        {
            Assert.False(string.IsNullOrWhiteSpace(tool.Name), "Tool name must not be empty");
            Assert.False(string.IsNullOrWhiteSpace(tool.Description), $"Tool {tool.Name} description must not be empty");
            Assert.NotNull(tool.ParametersSchema);
            Assert.Equal("object", tool.ParametersSchema["type"]?.GetValue<string>());
            Assert.NotNull(tool.ParametersSchema["properties"]);
            Assert.NotNull(tool.ParametersSchema["required"]);
        }
    }

    [Theory]
    [InlineData("spec_hub_list_documents", true)]
    [InlineData("spec_hub_read_document", true)]
    [InlineData("spec_hub_search", true)]
    [InlineData("backlog_get_stories", true)]
    [InlineData("sprint_get_history_and_velocity", true)]
    [InlineData("team_get_dev_capacities", true)]
    [InlineData("ai_sprint_plan_propose", true)]
    [InlineData("unknown_tool", false)]
    [InlineData("chat_completion", false)]
    public void HasTool_IdentifiesSupportedToolsCorrectly(string toolName, bool expected)
    {
        var actual = _registry.HasTool(toolName);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task ExecuteToolAsync_UnknownTool_ReturnsToolNotFoundJson()
    {
        var scope = new ProjectScopeContext(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var result = await _registry.ExecuteToolAsync("non_existent_tool", "{}", scope);

        Assert.Contains("TOOL_NOT_FOUND", result);
        Assert.Contains("không tồn tại", result);
    }

    [Fact]
    public async Task ExecuteToolAsync_MalformedJsonArguments_ReturnsInvalidArgumentsJson()
    {
        var scope = new ProjectScopeContext(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var result = await _registry.ExecuteToolAsync("spec_hub_list_documents", "{invalid-json}", scope);

        Assert.Contains("INVALID_JSON_ARGUMENTS", result);
    }
}
