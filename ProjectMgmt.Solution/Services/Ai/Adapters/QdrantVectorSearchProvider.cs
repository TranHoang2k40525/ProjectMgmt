namespace ProjectMgmt.Solution.Services.Ai.Adapters;

using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectMgmt.Core.VectorSearch;

public sealed class QdrantVectorSearchProvider : IExternalVectorSearchProvider
{
    private readonly QdrantOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<QdrantVectorSearchProvider> _logger;

    public QdrantVectorSearchProvider(
        IOptions<QdrantOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<QdrantVectorSearchProvider> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string ProviderName => "Qdrant";

    private HttpClient? CreateClient()
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.Host) || _options.Host.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var client = _httpClientFactory.CreateClient("QdrantClient");
        var scheme = _options.UseHttps ? "https" : "http";
        client.BaseAddress = new Uri($"{scheme}://{_options.Host}:{_options.Port}");
        client.Timeout = TimeSpan.FromMilliseconds(_options.TimeoutMs);

        if (!string.IsNullOrWhiteSpace(_options.ApiKey) && !_options.ApiKey.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase))
        {
            client.DefaultRequestHeaders.Remove("api-key");
            client.DefaultRequestHeaders.Add("api-key", _options.ApiKey);
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
            var res = await client.GetAsync("/readyz", ct);
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        Guid projectId,
        string query,
        int topK,
        CancellationToken ct = default)
    {
        using var client = CreateClient();
        if (client == null) return Array.Empty<VectorSearchResult>();

        var filterBody = new
        {
            limit = topK,
            with_payload = true,
            filter = new
            {
                must = new object[]
                {
                    new { key = "project_id", match = new { value = projectId.ToString() } }
                }
            }
        };

        var response = await client.PostAsJsonAsync($"/collections/{_options.CollectionName}/points/scroll", filterBody, ct);
        response.EnsureSuccessStatusCode();

        var jsonNode = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: ct);
        var points = jsonNode?["result"]?["points"]?.AsArray();
        if (points == null) return Array.Empty<VectorSearchResult>();

        var results = new List<VectorSearchResult>();
        foreach (var point in points)
        {
            var id = point?["id"]?.ToString() ?? Guid.NewGuid().ToString("N");
            var payload = point?["payload"]?.AsObject();
            if (payload == null) continue;

            var title = payload["title"]?.ToString() ?? "Untitled Spec";
            var content = payload["content"]?.ToString() ?? string.Empty;
            var source = payload["source_path"]?.ToString() ?? string.Empty;

            results.Add(new VectorSearchResult
            {
                Id = id,
                Title = title,
                ContentSnippet = content.Length > 300 ? content[..300] + "..." : content,
                SourcePath = source,
                Score = 0.95
            });
        }
        return results;
    }

    public async Task<bool> UpsertChunkAsync(
        Guid projectId,
        string chunkId,
        string title,
        string content,
        string sourcePath,
        IReadOnlyDictionary<string, string>? metadata,
        CancellationToken ct = default)
    {
        using var client = CreateClient();
        if (client == null) return false;

        var payload = new Dictionary<string, object?>
        {
            ["project_id"] = projectId.ToString(),
            ["title"] = title,
            ["content"] = content,
            ["source_path"] = sourcePath
        };
        if (metadata != null)
        {
            foreach (var kv in metadata) payload[kv.Key] = kv.Value;
        }

        var body = new
        {
            points = new[]
            {
                new { id = chunkId, payload = payload }
            }
        };

        var res = await client.PutAsJsonAsync($"/collections/{_options.CollectionName}/points", body, ct);
        return res.IsSuccessStatusCode;
    }
}
