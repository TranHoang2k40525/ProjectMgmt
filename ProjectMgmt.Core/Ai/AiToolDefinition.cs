namespace ProjectMgmt.Core.Ai;

using System.Text.Json.Nodes;

/// <summary>
/// Definition of a tool available for model function calling.
/// </summary>
public class AiToolDefinition
{
    /// <summary>
    /// Function name identifier (e.g. "spec_hub_read_document", "backlog_get_stories").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Natural language description of what the tool does and when the model should use it.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// JSON schema describing the tool parameters and required fields.
    /// </summary>
    public JsonObject ParametersSchema { get; set; } = new();

    public AiToolDefinition() { }

    public AiToolDefinition(string name, string description, JsonObject parametersSchema)
    {
        Name = name;
        Description = description;
        ParametersSchema = parametersSchema;
    }
}

/// <summary>
/// A completed tool call requested by the AI model.
/// </summary>
public class AiToolCall
{
    /// <summary>
    /// Provider-generated call identifier (e.g. "call_abc123").
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Name of the tool to be executed.
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// JSON-encoded string of arguments supplied by the model.
    /// </summary>
    public string ArgumentsJson { get; set; } = "{}";
}

/// <summary>
/// An incremental chunk of a tool call received during streaming.
/// </summary>
public class AiToolCallDelta
{
    /// <summary>
    /// Index of the tool call in parallel execution streams.
    /// </summary>
    public int? Index { get; set; }

    /// <summary>
    /// Call identifier if present in this chunk.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Tool name if present in this chunk.
    /// </summary>
    public string? ToolName { get; set; }

    /// <summary>
    /// Partial JSON string chunk of arguments.
    /// </summary>
    public string? ArgumentsDelta { get; set; }
}
