namespace ProjectMgmt.Solution.Services.Ai;

using System.IO;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectMgmt.Core.Ai;
using ProjectMgmt.Core.Security;

public sealed class MultiProviderAiModelClient : IAiModelClient
{
    private readonly IOptions<AiOptions> _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MultiProviderAiModelClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public MultiProviderAiModelClient(
        HttpClient httpClient,
        IOptions<AiOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<MultiProviderAiModelClient> logger)
        : this(options, httpClientFactory, logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
    }

    public MultiProviderAiModelClient(
        IOptions<AiOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<MultiProviderAiModelClient> logger)
    {
        _options = options;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string ProviderName => _options.Value.Provider;

    public async Task<AiCompletionResult> GenerateAsync(
        AiPrompt prompt,
        IProjectScopeContext scope,
        CancellationToken cancellationToken = default)
    {
        var contentBuilder = new StringBuilder();
        var thoughtBuilder = new StringBuilder();
        var toolCalls = new List<AiToolCall>();
        AiUsageInfo? finalUsage = null;
        string finishReason = "stop";

        await StreamGenerateAsync(
            prompt,
            scope,
            chunk =>
            {
                switch (chunk.ChunkType)
                {
                    case AiStreamChunkType.Content when !string.IsNullOrEmpty(chunk.DeltaContent):
                        contentBuilder.Append(chunk.DeltaContent);
                        break;
                    case AiStreamChunkType.Thought when !string.IsNullOrEmpty(chunk.DeltaThought):
                        thoughtBuilder.Append(chunk.DeltaThought);
                        break;
                    case AiStreamChunkType.ToolCall when chunk.ToolCallDeltas != null:
                        foreach (var delta in chunk.ToolCallDeltas)
                        {
                            var existing = toolCalls.FirstOrDefault(t => t.Id == delta.Id || (!string.IsNullOrEmpty(delta.ToolName) && t.ToolName == delta.ToolName));
                            if (existing == null)
                            {
                                toolCalls.Add(new AiToolCall
                                {
                                    Id = delta.Id ?? Guid.NewGuid().ToString("N"),
                                    ToolName = delta.ToolName ?? string.Empty,
                                    ArgumentsJson = delta.ArgumentsDelta ?? string.Empty
                                });
                            }
                            else
                            {
                                existing.ArgumentsJson += delta.ArgumentsDelta ?? string.Empty;
                            }
                        }
                        break;
                    case AiStreamChunkType.Done:
                        finalUsage = chunk.Usage;
                        finishReason = chunk.FinishReason ?? "stop";
                        break;
                }
                return Task.CompletedTask;
            },
            cancellationToken);

        var activeSettings = _options.Value.GetActiveProviderSettings();
        return AiCompletionResult.Success(
            content: contentBuilder.ToString(),
            thought: thoughtBuilder.Length > 0 ? thoughtBuilder.ToString() : null,
            usage: finalUsage,
            model: prompt.Model ?? activeSettings.ModelName,
            toolCalls: toolCalls.Count > 0 ? toolCalls : null);
    }

    public async Task StreamGenerateAsync(
        AiPrompt prompt,
        IProjectScopeContext scope,
        Func<AiStreamChunk, Task> onChunk,
        CancellationToken cancellationToken = default)
    {
        ProjectScopeGuard.Validate(scope, scope.OrganizationId, scope.ProjectId);
        var settings = _options.Value.GetActiveProviderSettings();
        var normalizedProvider = _options.Value.Provider?.Trim().ToLowerInvariant() ?? string.Empty;

        switch (normalizedProvider)
        {
            case "gemini":
                await StreamGeminiAsync(prompt, settings, onChunk, cancellationToken);
                break;
            case "claude":
                await StreamClaudeAsync(prompt, settings, onChunk, cancellationToken);
                break;
            case "openai":
                await StreamOpenAiAsync(prompt, settings, onChunk, cancellationToken);
                break;
            case "local" or "ollama":
                await StreamLocalOllamaAsync(prompt, settings, onChunk, cancellationToken);
                break;
            default:
                throw new NotSupportedException($"Nhà cung cấp AI '{_options.Value.Provider}' chưa được hỗ trợ.");
        }
    }

    private static string NormalizeGeminiModel(string? rawModel)
    {
        var m = (rawModel ?? string.Empty).Trim().ToLowerInvariant().Replace(" ", "-");
        if (m.StartsWith("models/")) m = m["models/".Length..];
        if (string.IsNullOrEmpty(m) || m.Contains("2.5") || m.Contains("2.0") || m.Contains("1.5"))
        {
            return "gemini-3-flash-preview";
        }
        return m;
    }

    private async Task StreamGeminiAsync(
        AiPrompt prompt,
        (string ApiKey, string Endpoint, string ModelName, int MaxTokens, double Temperature) settings,
        Func<AiStreamChunk, Task> onChunk,
        CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("AiClient");
        var primaryModel = NormalizeGeminiModel(prompt.Model ?? settings.ModelName);
        var candidateModels = new List<string> { primaryModel, "gemini-3-flash-preview", "gemini-3.8-flash", "gemini-3.5-flash" }.Distinct().ToList();

        Exception? lastException = null;
        var streamCompleted = false;

        foreach (var model in candidateModels)
        {
            var url = $"{settings.Endpoint.TrimEnd('/')}/v1beta/models/{model}:streamGenerateContent?alt=sse&key={settings.ApiKey}";

            var contents = new List<object>();
            foreach (var msg in prompt.Messages)
            {
                var role = msg.Role switch
                {
                    AiRole.Assistant => "model",
                    _ => "user"
                };
                contents.Add(new
                {
                    role,
                    parts = new object[] { new { text = msg.Content ?? string.Empty } }
                });
            }

            var requestBody = new Dictionary<string, object>
            {
                ["contents"] = contents,
                ["generationConfig"] = new
                {
                    temperature = prompt.Temperature ?? settings.Temperature,
                    maxOutputTokens = prompt.MaxTokens ?? settings.MaxTokens
                }
            };

            if (!string.IsNullOrWhiteSpace(prompt.SystemInstruction))
            {
                requestBody["systemInstruction"] = new
                {
                    parts = new object[] { new { text = prompt.SystemInstruction } }
                };
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = JsonContent.Create(requestBody)
                };
                request.Headers.TryAddWithoutValidation("x-goog-api-key", settings.ApiKey);

                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!response.IsSuccessStatusCode)
                {
                    var errBody = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogWarning("[GeminiStream] Model {Model} returned {StatusCode}: {ErrorBody}", model, response.StatusCode, errBody);
                    continue;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var reader = new StreamReader(stream, Encoding.UTF8);

                string? line;
                var hasReceivedAnyContent = false;
                while ((line = await reader.ReadLineAsync(ct)) != null)
                {
                    if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;
                    var dataJson = line[6..].Trim();
                    if (string.IsNullOrWhiteSpace(dataJson)) continue;

                    try
                    {
                        var doc = JsonNode.Parse(dataJson);
                        var candidates = doc?["candidates"]?.AsArray();
                        if (candidates != null && candidates.Count > 0)
                        {
                            var parts = candidates[0]?["content"]?["parts"]?.AsArray();
                            if (parts != null)
                            {
                                foreach (var part in parts)
                                {
                                    var text = part?["text"]?.GetValue<string>();
                                    var isThought = part?["thought"]?.GetValue<bool>() ?? false;

                                    if (!string.IsNullOrEmpty(text))
                                    {
                                        hasReceivedAnyContent = true;
                                        if (isThought)
                                        {
                                            await onChunk(AiStreamChunk.Thought(text));
                                        }
                                        else
                                        {
                                            await onChunk(AiStreamChunk.Content(text));
                                        }
                                    }
                                    else if (part?["functionCall"] != null)
                                    {
                                        var fn = part["functionCall"]!;
                                        var fnName = fn["name"]?.GetValue<string>() ?? string.Empty;
                                        var args = fn["args"]?.ToJsonString() ?? "{}";
                                        await onChunk(AiStreamChunk.ToolCall(fnName, Guid.NewGuid().ToString("N"), args));
                                    }
                                }
                            }
                        }

                        var usageNode = doc?["usageMetadata"];
                        if (usageNode != null)
                        {
                            var promptToks = usageNode["promptTokenCount"]?.GetValue<int>() ?? 0;
                            var candToks = usageNode["candidatesTokenCount"]?.GetValue<int>() ?? 0;
                            await onChunk(AiStreamChunk.Done("stop", new AiUsageInfo { PromptTokens = promptToks, CompletionTokens = candToks }));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[GeminiStream] Lỗi parse SSE chunk");
                    }
                }

                if (hasReceivedAnyContent)
                {
                    streamCompleted = true;
                    break;
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                lastException = ex;
                _logger.LogWarning(ex, "[GeminiStream] Thử nghiệm model {Model} thất bại, chuyển sang model dự phòng", model);
            }
        }

        if (!streamCompleted && lastException != null)
        {
            throw lastException;
        }
    }

    private async Task StreamClaudeAsync(
        AiPrompt prompt,
        (string ApiKey, string Endpoint, string ModelName, int MaxTokens, double Temperature) settings,
        Func<AiStreamChunk, Task> onChunk,
        CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("AiClient");
        var url = $"{settings.Endpoint.TrimEnd('/')}/v1/messages";

        var messages = prompt.Messages.Where(m => m.Role != AiRole.System).Select(m => new
        {
            role = m.Role == AiRole.Assistant ? "assistant" : "user",
            content = m.Content ?? string.Empty
        }).ToList();

        var requestBody = new Dictionary<string, object>
        {
            ["model"] = prompt.Model ?? settings.ModelName,
            ["max_tokens"] = prompt.MaxTokens ?? settings.MaxTokens,
            ["messages"] = messages,
            ["stream"] = true,
            ["temperature"] = prompt.Temperature ?? settings.Temperature
        };

        if (!string.IsNullOrWhiteSpace(prompt.SystemInstruction))
        {
            requestBody["system"] = prompt.SystemInstruction;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(requestBody)
        };
        request.Headers.Add("x-api-key", settings.ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;
            var dataJson = line[6..].Trim();
            if (string.IsNullOrWhiteSpace(dataJson)) continue;

            try
            {
                var doc = JsonNode.Parse(dataJson);
                var type = doc?["type"]?.GetValue<string>();

                if (type == "content_block_delta")
                {
                    var delta = doc?["delta"];
                    var deltaType = delta?["type"]?.GetValue<string>();
                    if (deltaType == "thinking_delta")
                    {
                        var thinking = delta?["thinking"]?.GetValue<string>();
                        if (!string.IsNullOrEmpty(thinking)) await onChunk(AiStreamChunk.Thought(thinking));
                    }
                    else if (deltaType == "text_delta")
                    {
                        var text = delta?["text"]?.GetValue<string>();
                        if (!string.IsNullOrEmpty(text)) await onChunk(AiStreamChunk.Content(text));
                    }
                    else if (deltaType == "input_json_delta")
                    {
                        var partialJson = delta?["partial_json"]?.GetValue<string>();
                        if (!string.IsNullOrEmpty(partialJson))
                        {
                            await onChunk(AiStreamChunk.ToolCall(string.Empty, string.Empty, partialJson));
                        }
                    }
                }
                else if (type == "message_delta")
                {
                    var usage = doc?["usage"];
                    var outToks = usage?["output_tokens"]?.GetValue<int>() ?? 0;
                    await onChunk(AiStreamChunk.Done("stop", new AiUsageInfo { CompletionTokens = outToks }));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ClaudeStream] Lỗi parse SSE chunk");
            }
        }
    }

    private async Task StreamOpenAiAsync(
        AiPrompt prompt,
        (string ApiKey, string Endpoint, string ModelName, int MaxTokens, double Temperature) settings,
        Func<AiStreamChunk, Task> onChunk,
        CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("AiClient");
        var url = $"{settings.Endpoint.TrimEnd('/')}/v1/chat/completions";

        var messages = new List<object>();
        if (!string.IsNullOrWhiteSpace(prompt.SystemInstruction))
        {
            messages.Add(new { role = "system", content = prompt.SystemInstruction });
        }
        foreach (var msg in prompt.Messages)
        {
            var role = msg.Role switch
            {
                AiRole.System => "system",
                AiRole.Assistant => "assistant",
                _ => "user"
            };
            messages.Add(new { role, content = msg.Content ?? string.Empty });
        }

        var requestBody = new Dictionary<string, object>
        {
            ["model"] = prompt.Model ?? settings.ModelName,
            ["messages"] = messages,
            ["stream"] = true,
            ["temperature"] = prompt.Temperature ?? settings.Temperature
        };
        if (prompt.MaxTokens.HasValue) requestBody["max_tokens"] = prompt.MaxTokens.Value;

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(requestBody)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;
            var dataJson = line[6..].Trim();
            if (dataJson == "[DONE]")
            {
                await onChunk(AiStreamChunk.Done("stop"));
                break;
            }

            try
            {
                var doc = JsonNode.Parse(dataJson);
                var choice = doc?["choices"]?[0];
                var delta = choice?["delta"];

                var reasoning = delta?["reasoning_content"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(reasoning))
                {
                    await onChunk(AiStreamChunk.Thought(reasoning));
                }

                var text = delta?["content"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(text))
                {
                    await onChunk(AiStreamChunk.Content(text));
                }

                var toolCallsNode = delta?["tool_calls"]?.AsArray();
                if (toolCallsNode != null)
                {
                    foreach (var tc in toolCallsNode)
                    {
                        var fn = tc?["function"];
                        var fnName = fn?["name"]?.GetValue<string>() ?? string.Empty;
                        var argsDelta = fn?["arguments"]?.GetValue<string>() ?? string.Empty;
                        var id = tc?["id"]?.GetValue<string>() ?? string.Empty;
                        await onChunk(AiStreamChunk.ToolCall(fnName, id, argsDelta));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[OpenAiStream] Lỗi parse SSE chunk");
            }
        }
    }

    private async Task StreamLocalOllamaAsync(
        AiPrompt prompt,
        (string ApiKey, string Endpoint, string ModelName, int MaxTokens, double Temperature) settings,
        Func<AiStreamChunk, Task> onChunk,
        CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("AiClient");
        var url = $"{settings.Endpoint.TrimEnd('/')}/api/chat";

        var messages = new List<object>();
        if (!string.IsNullOrWhiteSpace(prompt.SystemInstruction))
        {
            messages.Add(new { role = "system", content = prompt.SystemInstruction });
        }
        foreach (var msg in prompt.Messages)
        {
            var role = msg.Role switch
            {
                AiRole.System => "system",
                AiRole.Assistant => "assistant",
                _ => "user"
            };
            messages.Add(new { role, content = msg.Content ?? string.Empty });
        }

        var requestBody = new
        {
            model = prompt.Model ?? settings.ModelName,
            messages,
            stream = true,
            options = new
            {
                temperature = prompt.Temperature ?? settings.Temperature
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(requestBody)
        };
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        }

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        bool inThinkTag = false;
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var doc = JsonNode.Parse(line);
                var content = doc?["message"]?["content"]?.GetValue<string>();
                var isDone = doc?["done"]?.GetValue<bool>() ?? false;

                if (!string.IsNullOrEmpty(content))
                {
                    if (content.Contains("<think>"))
                    {
                        inThinkTag = true;
                        content = content.Replace("<think>", string.Empty);
                    }
                    if (content.Contains("</think>"))
                    {
                        inThinkTag = false;
                        content = content.Replace("</think>", string.Empty);
                    }

                    if (inThinkTag)
                    {
                        await onChunk(AiStreamChunk.Thought(content));
                    }
                    else
                    {
                        await onChunk(AiStreamChunk.Content(content));
                    }
                }

                if (isDone)
                {
                    var promptToks = doc?["prompt_eval_count"]?.GetValue<int>() ?? 0;
                    var evalToks = doc?["eval_count"]?.GetValue<int>() ?? 0;
                    await onChunk(AiStreamChunk.Done("stop", new AiUsageInfo { PromptTokens = promptToks, CompletionTokens = evalToks }));
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[OllamaStream] Lỗi parse NDJSON line");
            }
        }
    }
}
