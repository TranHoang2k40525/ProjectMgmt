namespace ProjectMgmt.Solution.Services.Ai.Adapters;

using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectMgmt.Core.Ai;
using ProjectMgmt.Core.VectorSearch;

public sealed class QdrantVectorSearchProvider : IExternalVectorSearchProvider
{
    private readonly QdrantOptions _options;
    private readonly AiOptions? _aiOptions;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<QdrantVectorSearchProvider> _logger;

    public QdrantVectorSearchProvider(
        IOptions<QdrantOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<QdrantVectorSearchProvider> logger,
        IOptions<AiOptions>? aiOptions = null)
    {
        _options = options.Value;
        _aiOptions = aiOptions?.Value;
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

    private async Task<float[]?> GenerateEmbeddingAsync(string text, CancellationToken ct)
    {
        if (_aiOptions == null || string.IsNullOrWhiteSpace(_aiOptions.Gemini.ApiKey) || _aiOptions.Gemini.ApiKey.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("AiClient");
            var endpoint = _aiOptions.Gemini.Endpoint.TrimEnd('/');
            var url = $"{endpoint}/v1beta/models/gemini-embedding-001:embedContent";

            var body = new
            {
                content = new
                {
                    parts = new[] { new { text } }
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(body)
            };
            request.Headers.TryAddWithoutValidation("x-goog-api-key", _aiOptions.Gemini.ApiKey);

            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: ct);
            var values = json?["embedding"]?["values"]?.AsArray();
            if (values == null) return null;

            var vector = new float[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                vector[i] = (float)(values[i]?.GetValue<double>() ?? 0.0);
            }
            return vector;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[QdrantVectorSearch] Không thể tạo vector embedding cho văn bản.");
            return null;
        }
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

        // 1. Tạo vector truy vấn thật từ nội dung câu hỏi
        var queryVector = await GenerateEmbeddingAsync(query, ct);
        if (queryVector != null && queryVector.Length > 0)
        {
            try
            {
                var searchBody = new
                {
                    vector = queryVector,
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

                var searchResponse = await client.PostAsJsonAsync($"/collections/{_options.CollectionName}/points/search", searchBody, ct);
                if (searchResponse.IsSuccessStatusCode)
                {
                    var searchJson = await searchResponse.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: ct);
                    var searchPoints = searchJson?["result"]?.AsArray();
                    if (searchPoints != null && searchPoints.Count > 0)
                    {
                        var vectorResults = new List<VectorSearchResult>();
                        foreach (var point in searchPoints)
                        {
                            var id = point?["id"]?.ToString() ?? Guid.NewGuid().ToString("N");
                            var score = point?["score"]?.GetValue<double>() ?? 0.0;
                            var payload = point?["payload"]?.AsObject();
                            if (payload == null) continue;

                            var title = payload["title"]?.ToString() ?? "Untitled Spec";
                            var content = payload["content"]?.ToString() ?? string.Empty;
                            var source = payload["source_path"]?.ToString() ?? string.Empty;

                            vectorResults.Add(new VectorSearchResult
                            {
                                Id = id,
                                Title = title,
                                ContentSnippet = content.Length > 300 ? content[..300] + "..." : content,
                                SourcePath = source,
                                Score = Math.Round(score, 4)
                            });
                        }
                        return vectorResults;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[QdrantVectorSearch] Vector search lỗi, chuyển sang fallback scroll.");
            }
        }

        // 2. Fallback dự phòng scroll nếu chưa sinh được vector
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
        if (!response.IsSuccessStatusCode) return Array.Empty<VectorSearchResult>();

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
                Score = 0.50
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

        // Tạo vector embedding cho đoạn văn bản để Qdrant index chuẩn xác
        var vector = await GenerateEmbeddingAsync($"{title}\n{content}", ct);

        object pointObj = vector != null
            ? new { id = chunkId, vector = vector, payload = payload }
            : new { id = chunkId, payload = payload };

        var body = new
        {
            points = new[] { pointObj }
        };

        var res = await client.PutAsJsonAsync($"/collections/{_options.CollectionName}/points", body, ct);
        return res.IsSuccessStatusCode;
    }
}
