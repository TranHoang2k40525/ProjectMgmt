using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectMgmt.Core.Resilience;

namespace ProjectMgmt.Core.Tracing;

/// <summary>
/// Resilient trace service.
/// Exports rich trace telemetry to Langfuse when available.
/// Falls back seamlessly to Serilog structured logging when Langfuse is offline or disabled.
/// Guarantee: Never throws an exception back to caller.
/// </summary>
public sealed class ResilientTraceService : IAiTraceService
{
    private readonly IExternalTraceExporter? _externalExporter;
    private readonly CircuitBreakerPolicy _circuitBreaker;
    private readonly ILogger<ResilientTraceService> _logger;
    private readonly bool _langfuseEnabled;

    public ResilientTraceService(
        IExternalTraceExporter? externalExporter = null,
        CircuitBreakerPolicy? circuitBreaker = null,
        bool langfuseEnabled = false,
        TimeSpan? timeout = null,
        ILogger<ResilientTraceService>? logger = null)
    {
        _externalExporter = externalExporter;
        _langfuseEnabled = langfuseEnabled && externalExporter != null;
        _logger = logger ?? NullLogger<ResilientTraceService>.Instance;
        _circuitBreaker = circuitBreaker ?? new CircuitBreakerPolicy(
            serviceName: "LangfuseTracing",
            failureThreshold: 3,
            breakDuration: TimeSpan.FromSeconds(60),
            executionTimeout: timeout ?? TimeSpan.FromMilliseconds(1000));
    }

    public async Task<string> StartTraceAsync(
        string traceId,
        Guid projectId,
        Guid userId,
        string sessionName,
        IReadOnlyDictionary<string, string>? tags = null,
        CancellationToken ct = default)
    {
        var session = new AiTraceSession
        {
            TraceId = traceId,
            ProjectId = projectId,
            UserId = userId,
            SessionName = sessionName,
            StartedAtUtc = DateTimeOffset.UtcNow,
            Tags = tags
        };

        // Fallback logging to Serilog
        _logger.LogInformation(
            "[AI-TRACE] Started Session: TraceId={TraceId} | ProjectId={ProjectId} | UserId={UserId} | Name={SessionName}",
            traceId, projectId, userId, sessionName);

        if (_langfuseEnabled && _externalExporter != null && _circuitBreaker.AllowExecution())
        {
            try
            {
                await _circuitBreaker.ExecuteWithFallbackAsync(
                    primaryAction: token => _externalExporter.ExportTraceAsync(session, token),
                    fallbackAction: token => Task.CompletedTask,
                    ct: ct,
                    onFallback: (reason, ex) =>
                    {
                        _logger.LogWarning(ex, "[ResilientTrace] {Reason} TraceId={TraceId}", reason, traceId);
                    }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ResilientTrace] Swallow trace export error for TraceId={TraceId}", traceId);
            }
        }

        return traceId;
    }

    public async Task RecordSpanAsync(AiTraceSpan span, CancellationToken ct = default)
    {
        // Fallback Serilog structured logging
        _logger.LogInformation(
            "[AI-TRACE-SPAN] TraceId={TraceId} | Span={SpanName} ({SpanType}) | Status={Status} | Duration={DurationMs}ms | Tokens={TotalTokens} ({PromptTokens}/{CompletionTokens})",
            span.TraceId, span.Name, span.SpanType, span.Status, span.DurationMs, span.TotalTokens, span.PromptTokens, span.CompletionTokens);

        if (_langfuseEnabled && _externalExporter != null && _circuitBreaker.AllowExecution())
        {
            try
            {
                await _circuitBreaker.ExecuteWithFallbackAsync(
                    primaryAction: token => _externalExporter.ExportSpanAsync(span, token),
                    fallbackAction: token => Task.CompletedTask,
                    ct: ct,
                    onFallback: (reason, ex) =>
                    {
                        _logger.LogWarning(ex, "[ResilientTrace] {Reason} SpanId={SpanId}", reason, span.SpanId);
                    }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ResilientTrace] Swallow span export error for SpanId={SpanId}", span.SpanId);
            }
        }
    }

    public async Task RecordGenerationAsync(
        string traceId,
        string modelName,
        string prompt,
        string completion,
        int promptTokens,
        int completionTokens,
        long durationMs,
        IReadOnlyDictionary<string, object?>? metadata = null,
        CancellationToken ct = default)
    {
        var span = new AiTraceSpan
        {
            SpanId = Guid.NewGuid().ToString("N"),
            TraceId = traceId,
            Name = $"generation:{modelName}",
            SpanType = "generation",
            StartedAtUtc = DateTimeOffset.UtcNow.AddMilliseconds(-durationMs),
            EndedAtUtc = DateTimeOffset.UtcNow,
            DurationMs = durationMs,
            Input = prompt,
            Output = completion,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            TotalTokens = promptTokens + completionTokens,
            Status = "Success",
            Metadata = metadata
        };

        await RecordSpanAsync(span, ct).ConfigureAwait(false);
    }

    public async Task RecordToolCallAsync(
        string traceId,
        string toolName,
        string inputJson,
        string outputJson,
        long durationMs,
        bool success,
        string? errorMessage = null,
        CancellationToken ct = default)
    {
        var span = new AiTraceSpan
        {
            SpanId = Guid.NewGuid().ToString("N"),
            TraceId = traceId,
            Name = $"tool:{toolName}",
            SpanType = "tool_call",
            StartedAtUtc = DateTimeOffset.UtcNow.AddMilliseconds(-durationMs),
            EndedAtUtc = DateTimeOffset.UtcNow,
            DurationMs = durationMs,
            Input = inputJson,
            Output = outputJson,
            Status = success ? "Success" : "Failed",
            ErrorMessage = errorMessage
        };

        await RecordSpanAsync(span, ct).ConfigureAwait(false);
    }

    public Task RecordFeedbackAsync(
        string traceId,
        string feedbackType,
        decimal score,
        string? comment = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[AI-TRACE-FEEDBACK] TraceId={TraceId} | Type={FeedbackType} | Score={Score} | Comment={Comment}",
            traceId, feedbackType, score, comment);

        return Task.CompletedTask;
    }
}
