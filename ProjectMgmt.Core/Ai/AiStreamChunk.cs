namespace ProjectMgmt.Core.Ai;

/// <summary>
/// Discriminator for streaming chunk events.
/// </summary>
public enum AiStreamChunkType
{
    Content = 1,
    Thought = 2,
    ToolCall = 3,
    Usage = 4,
    Done = 5,
    Error = 6
}

/// <summary>
/// Token consumption metrics for prompt and generation.
/// </summary>
public class AiUsageInfo
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens => PromptTokens + CompletionTokens;
}

/// <summary>
/// Incremental streaming data emitted by IAiModelClient during generation.
/// </summary>
public class AiStreamChunk
{
    /// <summary>
    /// Type of this streaming chunk.
    /// </summary>
    public AiStreamChunkType ChunkType { get; set; } = AiStreamChunkType.Content;

    /// <summary>
    /// Text token delta for user-facing content generation.
    /// </summary>
    public string? DeltaContent { get; set; }

    /// <summary>
    /// Reasoning or internal thought token delta (e.g. Gemini 2.0 Thinking or Claude reasoning).
    /// </summary>
    public string? DeltaThought { get; set; }

    /// <summary>
    /// Tool call invocation deltas (tool name, call ID, argument fragment).
    /// </summary>
    public IReadOnlyList<AiToolCallDelta>? ToolCallDeltas { get; set; }

    /// <summary>
    /// Token usage metrics (typically emitted with or near the final Done chunk).
    /// </summary>
    public AiUsageInfo? Usage { get; set; }

    /// <summary>
    /// Provider finish reason (e.g. "stop", "tool_calls", "length", "content_filter").
    /// </summary>
    public string? FinishReason { get; set; }

    /// <summary>
    /// Error message if ChunkType == Error.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Sequence index of the chunk.
    /// </summary>
    public int? Index { get; set; }

    public static AiStreamChunk Content(string delta, int? index = null) =>
        new() { ChunkType = AiStreamChunkType.Content, DeltaContent = delta, Index = index };

    public static AiStreamChunk Thought(string delta, int? index = null) =>
        new() { ChunkType = AiStreamChunkType.Thought, DeltaThought = delta, Index = index };

    public static AiStreamChunk ToolCall(string toolName, string toolCallId, string argsDelta, int? index = null) =>
        new()
        {
            ChunkType = AiStreamChunkType.ToolCall,
            ToolCallDeltas = new List<AiToolCallDelta>
            {
                new() { ToolName = toolName, Id = toolCallId, ArgumentsDelta = argsDelta, Index = index }
            },
            Index = index
        };

    public static AiStreamChunk Done(string? finishReason = "stop", AiUsageInfo? usage = null) =>
        new() { ChunkType = AiStreamChunkType.Done, FinishReason = finishReason, Usage = usage };

    public static AiStreamChunk Error(string errorMessage) =>
        new() { ChunkType = AiStreamChunkType.Error, ErrorMessage = errorMessage };
}
