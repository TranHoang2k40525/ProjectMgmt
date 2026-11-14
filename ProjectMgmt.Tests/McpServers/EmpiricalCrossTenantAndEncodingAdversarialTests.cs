namespace ProjectMgmt.Tests.McpServers;

using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectMgmt.Core.Security;
using ProjectMgmt.Solution.McpServers;
using Xunit;

public class EmpiricalCrossTenantAndEncodingAdversarialTests
{
    private readonly McpToolRegistry _registry;
    private readonly Guid _callerOrgId = Guid.NewGuid();
    private readonly Guid _callerProjectId = Guid.NewGuid();
    private readonly Guid _callerUserId = Guid.NewGuid();
    private readonly ProjectScopeContext _callerScope;

    public EmpiricalCrossTenantAndEncodingAdversarialTests()
    {
        // Notice: DbContexts are deliberately passed as null!.
        // If any tool queries the database BEFORE scope validation,
        // it will crash with NullReferenceException (TOOL_EXECUTION_ERROR)
        // instead of throwing ProjectScopeException (PROJECT_SCOPE_VIOLATION).
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

        _callerScope = new ProjectScopeContext(_callerOrgId, _callerProjectId, _callerUserId);
    }

    [Theory]
    [InlineData("spec_hub_list_documents")]
    [InlineData("spec_hub_search")]
    [InlineData("backlog_get_stories")]
    [InlineData("project_get_active_config")]
    [InlineData("sprint_get_history_and_velocity")]
    [InlineData("team_get_dev_capacities")]
    [InlineData("ai_sprint_plan_propose")]
    public async Task CrossTenantProbe_ForeignProjectId_ThrowsScopeViolationBeforeDbQuery(string toolName)
    {
        var foreignProjectId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new
        {
            projectId = foreignProjectId,
            keyword = "authentication",
            meetingNotes = "Sprint 1 planning notes"
        });

        // Act
        var resultJson = await _registry.ExecuteToolAsync(toolName, payload, _callerScope);

        // Assert
        Assert.NotNull(resultJson);
        using var doc = JsonDocument.Parse(resultJson);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("error", out var errorProp));
        Assert.Equal("PROJECT_SCOPE_VIOLATION", errorProp.GetString());

        Assert.True(root.TryGetProperty("message", out var msgProp));
        var message = msgProp.GetString();
        Assert.NotNull(message);
        Assert.Contains("Truy cập bị từ chối", message);
        Assert.Contains("không thuộc ngữ cảnh", message);

        // Ensure Vietnamese message is NOT escaped as Unicode escapes (\u00f4, \u1ed3, etc.)
        Assert.DoesNotContain("\\u0", resultJson);
        Assert.DoesNotContain("\\u1", resultJson);
        Assert.Contains("Truy cập bị từ chối", resultJson);
    }

    [Theory]
    [InlineData("spec_hub_list_documents", "{\"projectId\": \"not-a-valid-guid\"}")]
    [InlineData("spec_hub_list_documents", "{\"projectId\": 999999}")]
    [InlineData("spec_hub_list_documents", "{\"projectId\": true}")]
    [InlineData("spec_hub_list_documents", "{\"projectId\": false}")]
    [InlineData("spec_hub_list_documents", "{\"projectId\": null}")]
    [InlineData("spec_hub_list_documents", "{\"projectId\": [\"a\", \"b\"]}")]
    [InlineData("spec_hub_list_documents", "{\"projectId\": {\"id\": \"00000000-0000-0000-0000-000000000000\"}}")]
    [InlineData("spec_hub_read_document", "{\"documentId\": \"not-a-valid-guid\"}")]
    [InlineData("spec_hub_read_document", "{\"documentId\": 123456}")]
    [InlineData("spec_hub_read_document", "{\"documentId\": false}")]
    [InlineData("spec_hub_read_document", "{\"documentId\": null}")]
    [InlineData("spec_hub_read_document", "{\"documentId\": {\"nested\": true}}")]
    [InlineData("spec_hub_search", "{\"projectId\": 123, \"keyword\": 456}")]
    [InlineData("spec_hub_search", "{\"projectId\": null, \"keyword\": null}")]
    [InlineData("spec_hub_search", "{\"projectId\": true, \"keyword\": false}")]
    [InlineData("backlog_get_stories", "{\"projectId\": 123}")]
    [InlineData("backlog_get_stories", "{\"projectId\": null}")]
    [InlineData("backlog_get_stories", "{\"projectId\": [1,2,3]}")]
    [InlineData("project_get_active_config", "{\"projectId\": 123}")]
    [InlineData("project_get_active_config", "{\"projectId\": null}")]
    [InlineData("sprint_get_history_and_velocity", "{\"projectId\": 123}")]
    [InlineData("sprint_get_history_and_velocity", "{\"projectId\": null}")]
    [InlineData("team_get_dev_capacities", "{\"projectId\": 123}")]
    [InlineData("team_get_dev_capacities", "{\"projectId\": null}")]
    [InlineData("ai_sprint_plan_propose", "{\"projectId\": 123, \"meetingNotes\": 456}")]
    [InlineData("ai_sprint_plan_propose", "{\"projectId\": null, \"meetingNotes\": null, \"targetStoryIds\": \"not_array\"}")]
    [InlineData("ai_sprint_plan_propose", "{\"projectId\": true, \"meetingNotes\": false, \"targetStoryIds\": [1, 2, 3]}")]
    public async Task MalformedArgumentTypes_DoNotCrash_ReturnsStructuredErrorJson(string toolName, string malformedPayload)
    {
        var ex = await Record.ExceptionAsync(() => _registry.ExecuteToolAsync(toolName, malformedPayload, _callerScope));
        Assert.Null(ex);

        var resultJson = await _registry.ExecuteToolAsync(toolName, malformedPayload, _callerScope);
        Assert.NotNull(resultJson);

        using var doc = JsonDocument.Parse(resultJson);
        Assert.True(doc.RootElement.TryGetProperty("error", out var errorProp));
        var error = errorProp.GetString();
        Assert.False(string.IsNullOrWhiteSpace(error));
        // Error must be structured and expected
        Assert.Contains(error, new[] { "INVALID_PROJECT_ID", "INVALID_DOCUMENT_ID", "EMPTY_KEYWORD", "EMPTY_MEETING_NOTES", "INVALID_JSON_ARGUMENTS" });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("123")]
    [InlineData("true")]
    [InlineData("\"string-literal\"")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{invalid-json-content}")]
    public async Task ExecuteToolAsync_NonStandardArgumentsJson_DoesNotCrash(string argumentsJson)
    {
        var ex = await Record.ExceptionAsync(() =>
            _registry.ExecuteToolAsync("spec_hub_list_documents", argumentsJson, _callerScope));
        Assert.Null(ex);

        var resultJson = await _registry.ExecuteToolAsync("spec_hub_list_documents", argumentsJson, _callerScope);
        Assert.NotNull(resultJson);

        using var doc = JsonDocument.Parse(resultJson);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task VietnameseUnicodePreservation_AllErrorsRetainNativeUtf8()
    {
        // 1. Unknown tool
        var r1 = await _registry.ExecuteToolAsync("unknown_tool_xyz", "{}", _callerScope);
        Assert.Contains("không tồn tại", r1);
        Assert.Contains("Công cụ", r1);
        Assert.DoesNotContain("\\u00", r1);

        // 2. Invalid JSON
        var r2 = await _registry.ExecuteToolAsync("spec_hub_list_documents", "{malformed:", _callerScope);
        Assert.Contains("không phải JSON hợp lệ", r2);
        Assert.DoesNotContain("\\u00", r2);

        // 3. Empty ProjectId
        var r3 = await _registry.ExecuteToolAsync("spec_hub_list_documents", "{\"projectId\": \"00000000-0000-0000-0000-000000000000\"}", _callerScope);
        Assert.Contains("không hợp lệ", r3);
        Assert.DoesNotContain("\\u00", r3);

        // 4. Empty DocumentId
        var r4 = await _registry.ExecuteToolAsync("spec_hub_read_document", "{\"documentId\": \"00000000-0000-0000-0000-000000000000\"}", _callerScope);
        Assert.Contains("không hợp lệ", r4);
        Assert.DoesNotContain("\\u00", r4);

        // 5. Empty keyword for valid project
        var r5 = await _registry.ExecuteToolAsync("spec_hub_search",
            $"{{\"projectId\": \"{_callerProjectId}\", \"keyword\": \"   \"}}", _callerScope);
        Assert.Contains("Vui lòng nhập từ khóa tìm kiếm.", r5);
        Assert.DoesNotContain("\\u00", r5);

        // 6. Empty meeting notes for valid project
        var r6 = await _registry.ExecuteToolAsync("ai_sprint_plan_propose",
            $"{{\"projectId\": \"{_callerProjectId}\", \"meetingNotes\": \"   \"}}", _callerScope);
        Assert.Contains("Vui lòng nhập ghi chú cuộc họp.", r6);
        Assert.DoesNotContain("\\u00", r6);

        // 7. Scope violation
        var foreignProject = Guid.NewGuid();
        var r7 = await _registry.ExecuteToolAsync("spec_hub_list_documents",
            $"{{\"projectId\": \"{foreignProject}\"}}", _callerScope);
        Assert.Contains("Truy cập bị từ chối", r7);
        Assert.Contains("không thuộc ngữ cảnh", r7);
        Assert.DoesNotContain("\\u00", r7);
    }

    [Fact]
    public async Task ConcurrentToolExecution_ThreadSafetyProbe_NoDataCrossContamination()
    {
        var tasks = Enumerable.Range(0, 50).Select(async i =>
        {
            var isForeign = (i % 2 == 0);
            var projectId = isForeign ? Guid.NewGuid() : _callerProjectId;
            var payload = $"{{\"projectId\": \"{projectId}\"}}";

            var result = await _registry.ExecuteToolAsync("backlog_get_stories", payload, _callerScope);
            using var doc = JsonDocument.Parse(result);

            if (isForeign)
            {
                Assert.Equal("PROJECT_SCOPE_VIOLATION", doc.RootElement.GetProperty("error").GetString());
            }
            else
            {
                // When project matches caller scope, it proceeds past scope validation to DB query.
                // Since DB context is null, it throws NullReferenceException handled as TOOL_EXECUTION_ERROR.
                Assert.Equal("TOOL_EXECUTION_ERROR", doc.RootElement.GetProperty("error").GetString());
            }
        });

        await Task.WhenAll(tasks);
    }
}
