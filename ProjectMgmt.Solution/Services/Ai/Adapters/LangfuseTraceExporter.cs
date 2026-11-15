namespace ProjectMgmt.Solution.Services.Ai.Adapters;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectMgmt.Core.Tracing;

public sealed class LangfuseTraceExporter : IExternalTraceExporter
{
    private readonly LangfuseOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<LangfuseTraceExporter> _logger;

    public LangfuseTraceExporter(
        IOptions<LangfuseOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<LangfuseTraceExporter> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string ExporterName => "Langfuse";

    private HttpClient? CreateClient()
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.Host) || _options.Host.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var client = _httpClientFactory.CreateClient("LangfuseClient");
        client.BaseAddress = new Uri(_options.Host.TrimEnd('/'));
        client.Timeout = TimeSpan.FromMilliseconds(_options.TimeoutMs);

        if (!string.IsNullOrWhiteSpace(_options.PublicKey) && !string.IsNullOrWhiteSpace(_options.SecretKey) &&
            !_options.PublicKey.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase))
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.PublicKey}:{_options.SecretKey}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }

        return client;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (!_options.Enabled) return false;
        try
        {
            using var client = CreateClient();
            if (client == null) return false;
            var response = await client.GetAsync("/api/public/health", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task ExportTraceAsync(AiTraceSession session, CancellationToken ct = default)
    {
        using var client = CreateClient();
        if (client == null) return;

        var payload = new
        {
            batch = new[]
            {
                new
                {
                    id = Guid.NewGuid().ToString("N"),
                    type = "trace-create",
                    timestamp = session.StartedAtUtc.ToString("o"),
                    body = new
                    {
                        id = session.TraceId,
                        name = session.SessionName,
                        userId = session.UserId.ToString(),
                        projectId = session.ProjectId.ToString(),
                        release = _options.Environment,
                        tags = session.Tags?.Select(t => $"{t.Key}:{t.Value}").ToList()
                    }
                }
            }
        };

        var response = await client.PostAsJsonAsync("/api/public/ingestion", payload, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task ExportSpanAsync(AiTraceSpan span, CancellationToken ct = default)
    {
        using var client = CreateClient();
        if (client == null) return;

        var isGeneration = span.SpanType == "generation";
        var eventType = isGeneration ? "generation-create" : "span-create";

        var body = isGeneration
            ? (object)new
            {
                id = span.SpanId,
                traceId = span.TraceId,
                name = span.Name,
                startTime = span.StartedAtUtc.ToString("o"),
                endTime = span.EndedAtUtc?.ToString("o"),
                input = span.Input,
                output = span.Output,
                statusMessage = span.ErrorMessage,
                level = span.Status == "Failed" ? "ERROR" : "DEFAULT",
                model = span.Name.Replace("generation:", ""),
                usage = new { promptTokens = span.PromptTokens ?? 0, completionTokens = span.CompletionTokens ?? 0, totalTokens = span.TotalTokens ?? 0 },
                metadata = span.Metadata
            }
            : new
            {
                id = span.SpanId,
                traceId = span.TraceId,
                name = span.Name,
                startTime = span.StartedAtUtc.ToString("o"),
                endTime = span.EndedAtUtc?.ToString("o"),
                input = span.Input,
                output = span.Output,
                statusMessage = span.ErrorMessage,
                level = span.Status == "Failed" ? "ERROR" : "DEFAULT",
                metadata = span.Metadata
            };

        var payload = new
        {
            batch = new[]
            {
                new
                {
                    id = Guid.NewGuid().ToString("N"),
                    type = eventType,
                    timestamp = span.StartedAtUtc.ToString("o"),
                    body
                }
            }
        };

        var response = await client.PostAsJsonAsync("/api/public/ingestion", payload, ct);
        response.EnsureSuccessStatusCode();
    }
}
