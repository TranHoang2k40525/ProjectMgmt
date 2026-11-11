namespace ProjectMgmt.Core.Ai;

/// <summary>
/// Role of the message participant in an AI conversation.
/// </summary>
public enum AiRole
{
    System = 1,
    User = 2,
    Assistant = 3,
    Tool = 4
}

/// <summary>
/// Represents a single message in the AI conversation.
/// </summary>
public class AiMessage
{
    /// <summary>
    /// Role of the sender.
    /// </summary>
    public AiRole Role { get; set; } = AiRole.User;

    /// <summary>
    /// Textual content of the message.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Author name or tool name associated with this message.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// ID of the tool call this message is responding to (when Role == Tool).
    /// </summary>
    public string? ToolCallId { get; set; }

    /// <summary>
    /// Tool calls requested by the assistant in this message (when Role == Assistant).
    /// </summary>
    public IReadOnlyList<AiToolCall>? ToolCalls { get; set; }

    /// <summary>
    /// Timestamp when the message was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public AiMessage() { }

    public AiMessage(AiRole role, string? content)
    {
        Role = role;
        Content = content;
    }

    public static AiMessage CreateSystem(string content) =>
        new(AiRole.System, content);

    public static AiMessage CreateUser(string content) =>
        new(AiRole.User, content);

    public static AiMessage CreateAssistant(string? content, IReadOnlyList<AiToolCall>? toolCalls = null) =>
        new(AiRole.Assistant, content) { ToolCalls = toolCalls };

    public static AiMessage CreateToolResult(string toolCallId, string toolName, string resultJson) =>
        new(AiRole.Tool, resultJson) { ToolCallId = toolCallId, Name = toolName };
}
