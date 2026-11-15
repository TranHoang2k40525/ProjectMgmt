namespace ProjectMgmt.Core.Tracing;

/// <summary>
/// External exporter for remote telemetry platforms (e.g. Langfuse HTTP API).
/// </summary>
public interface IExternalTraceExporter
{
    string ExporterName { get; }
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
    Task ExportTraceAsync(AiTraceSession session, CancellationToken ct = default);
    Task ExportSpanAsync(AiTraceSpan span, CancellationToken ct = default);
}
