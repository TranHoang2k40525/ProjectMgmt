namespace ProjectMgmt.Core.Tracing;

public sealed record AiTraceSession
{
    public required string TraceId { get; init; }
    public required Guid ProjectId { get; init; }
    public required Guid UserId { get; init; }
    public required string SessionName { get; init; }
    public DateTimeOffset StartedAtUtc { get; init; }
    public IReadOnlyDictionary<string, string>? Tags { get; init; }
}

public sealed record AiTraceSpan
{
    public required string SpanId { get; init; }
    public required string TraceId { get; init; }
    public required string Name { get; init; }
    public required string SpanType { get; init; } // "generation", "tool_call", "agent_step", "human_feedback"
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset? EndedAtUtc { get; init; }
    public long? DurationMs { get; init; }
    public string? Input { get; init; }
    public string? Output { get; init; }
    public int? PromptTokens { get; init; }
    public int? CompletionTokens { get; init; }
    public int? TotalTokens { get; init; }
    public string? Status { get; init; } // "Success", "Failed", "Pending"
    public string? ErrorMessage { get; init; }
    public IReadOnlyDictionary<string, object?>? Metadata { get; init; }
}

/// <summary>
/// Contract for observability tracing (Langfuse primary with Serilog structured fallback).
/// Tracing operations must never throw unhandled exceptions or interrupt agent processing.
/// </summary>
public interface IAiTraceService
{
    Task<string> StartTraceAsync(
        string traceId,
        Guid projectId,
        Guid userId,
        string sessionName,
        IReadOnlyDictionary<string, string>? tags = null,
        CancellationToken ct = default);

    Task RecordSpanAsync(
        AiTraceSpan span,
        CancellationToken ct = default);

    Task RecordGenerationAsync(
        string traceId,
        string modelName,
        string prompt,
        string completion,
        int promptTokens,
        int completionTokens,
        long durationMs,
        IReadOnlyDictionary<string, object?>? metadata = null,
        CancellationToken ct = default);

    Task RecordToolCallAsync(
        string traceId,
        string toolName,
        string inputJson,
        string outputJson,
        long durationMs,
        bool success,
        string? errorMessage = null,
        CancellationToken ct = default);

    Task RecordFeedbackAsync(
        string traceId,
        string feedbackType,
        decimal score,
        string? comment = null,
        CancellationToken ct = default);
}
