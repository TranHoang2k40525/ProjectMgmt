namespace ProjectMgmt.Solution.McpServers;

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ProjectMgmt.Core.Ai;
using ProjectMgmt.Core.Security;

public class McpToolRegistry : IMcpToolRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    private static string SerializeJson<T>(T value) =>
        JsonSerializer.Serialize(value, JsonOptions);

    private readonly SpecHubTools _specHub;
    private readonly BacklogTools _backlog;
    private readonly SprintTools _sprint;
    private readonly TeamTools _team;
    private readonly PlanningTools _planning;
    private readonly ILogger<McpToolRegistry> _logger;

    public McpToolRegistry(
        SpecHubTools specHub,
        BacklogTools backlog,
        SprintTools sprint,
        TeamTools team,
        PlanningTools planning,
        ILogger<McpToolRegistry> logger)
    {
        _specHub = specHub;
        _backlog = backlog;
        _sprint = sprint;
        _team = team;
        _planning = planning;
        _logger = logger;
    }

    public IReadOnlyList<AiToolDefinition> GetToolDefinitions()
    {
        var tools = new List<AiToolDefinition>();
        tools.AddRange(_specHub.GetToolDefinitions());
        tools.AddRange(_backlog.GetToolDefinitions());
        tools.AddRange(_sprint.GetToolDefinitions());
        tools.AddRange(_team.GetToolDefinitions());
        tools.AddRange(_planning.GetToolDefinitions());
        return tools;
    }

    public bool HasTool(string toolName)
    {
        return toolName switch
        {
            "spec_hub_list_documents" or
            "spec_hub_read_document" or
            "spec_hub_search" or
            "backlog_get_stories" or
            "sprint_get_history_and_velocity" or
            "team_get_dev_capacities" or
            "ai_sprint_plan_propose" => true,
            _ => false
        };
    }

    public async Task<string> ExecuteToolAsync(
        string toolName,
        string argumentsJson,
        IProjectScopeContext scope,
        CancellationToken cancellationToken = default)
    {
        JsonObject args;
        try
        {
            args = string.IsNullOrWhiteSpace(argumentsJson)
                ? new JsonObject()
                : JsonNode.Parse(argumentsJson) as JsonObject ?? new JsonObject();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[McpToolRegistry] JSON arguments parsing failed for tool {ToolName}", toolName);
            return SerializeJson(new { error = "INVALID_JSON_ARGUMENTS", message = "Dữ liệu tham số không phải JSON hợp lệ." });
        }

        try
        {
            switch (toolName)
            {
                case "spec_hub_list_documents":
                {
                    var projectId = ParseGuid(args["projectId"]);
                    return await _specHub.ListDocumentsAsync(projectId, scope, cancellationToken);
                }

                case "spec_hub_read_document":
                {
                    var documentId = ParseGuid(args["documentId"]);
                    return await _specHub.ReadDocumentAsync(documentId, scope, cancellationToken);
                }

                case "spec_hub_search":
                {
                    var projectId = ParseGuid(args["projectId"]);
                    var keyword = ParseString(args["keyword"]);
                    return await _specHub.SearchAsync(projectId, keyword, scope, cancellationToken);
                }

                case "backlog_get_stories":
                {
                    var projectId = ParseGuid(args["projectId"]);
                    return await _backlog.GetStoriesAsync(projectId, scope, cancellationToken);
                }

                case "sprint_get_history_and_velocity":
                {
                    var projectId = ParseGuid(args["projectId"]);
                    return await _sprint.GetHistoryAndVelocityAsync(projectId, scope, cancellationToken);
                }

                case "team_get_dev_capacities":
                {
                    var projectId = ParseGuid(args["projectId"]);
                    return await _team.GetDevCapacitiesAsync(projectId, scope, cancellationToken);
                }

                case "ai_sprint_plan_propose":
                {
                    var projectId = ParseGuid(args["projectId"]);
                    var meetingNotes = ParseString(args["meetingNotes"]);
                    var targetStoryIds = ParseGuidList(args["targetStoryIds"]);
                    return await _planning.ProposeSprintPlanAsync(projectId, meetingNotes, targetStoryIds, scope, cancellationToken);
                }

                default:
                    _logger.LogWarning("[McpToolRegistry] Unknown tool requested: {ToolName}", toolName);
                    return SerializeJson(new { error = "TOOL_NOT_FOUND", message = $"Công cụ '{toolName}' không tồn tại trong hệ thống." });
            }
        }
        catch (ProjectScopeException ex)
        {
            _logger.LogWarning(ex, "[McpToolRegistry] Scope violation executing tool {ToolName}", toolName);
            return SerializeJson(new { error = ex.ErrorCode ?? "PROJECT_SCOPE_VIOLATION", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[McpToolRegistry] Error executing tool {ToolName}", toolName);
            return SerializeJson(new { error = "TOOL_EXECUTION_ERROR", message = $"Lỗi thực thi công cụ '{toolName}': {ex.Message}" });
        }
    }

    private static string? GetStringSafe(JsonNode? node)
    {
        if (node == null) return null;
        if (node is JsonValue val)
        {
            if (val.TryGetValue<string>(out var strVal))
            {
                return strVal;
            }
            return val.ToString();
        }
        return node.ToJsonString();
    }

    private static Guid ParseGuid(JsonNode? node)
    {
        if (node == null) return Guid.Empty;
        if (node is JsonValue val)
        {
            if (val.TryGetValue<Guid>(out var g)) return g;
            if (val.TryGetValue<string>(out var str) && Guid.TryParse(str, out var parsed)) return parsed;
        }
        var s = GetStringSafe(node);
        return Guid.TryParse(s, out var result) ? result : Guid.Empty;
    }

    private static string ParseString(JsonNode? node, string defaultValue = "")
    {
        return GetStringSafe(node) ?? defaultValue;
    }

    private static List<Guid>? ParseGuidList(JsonNode? node)
    {
        if (node is not JsonArray array) return null;
        var list = new List<Guid>();
        foreach (var item in array)
        {
            var g = ParseGuid(item);
            if (g != Guid.Empty)
            {
                list.Add(g);
            }
        }
        return list;
    }
}
