namespace ProjectMgmt.Solution.McpServers;

using ProjectMgmt.Core.Ai;
using ProjectMgmt.Core.Security;

public interface IMcpToolRegistry
{
    IReadOnlyList<AiToolDefinition> GetToolDefinitions();
    Task<string> ExecuteToolAsync(
        string toolName,
        string argumentsJson,
        IProjectScopeContext scope,
        CancellationToken cancellationToken = default);
    bool HasTool(string toolName);
}
