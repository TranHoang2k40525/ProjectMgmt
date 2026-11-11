using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace DeliveryIntelligence.Infrastructure.Services.Rag;

public interface IEmbeddingClient
{
    /// <summary>Tên model ghi kèm mỗi đoạn; đổi model thì chỉ mục cũ không dùng chung được.</summary>
    string Model { get; }
    Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken ct = default);
}

public interface IImageTextReader
{
    bool IsConfigured { get; }
    /// <summary>Đọc toàn bộ chữ trong ảnh và mô tả nội dung (sơ đồ, bảng, màn hình) thành văn bản.</summary>
    Task<string> ReadAsync(byte[] image, string mimeType, CancellationToken ct = default);
}

/// <summary>Lỗi từ API AI; thông điệp không chứa key hay nội dung tài liệu.</summary>
public sealed class RagProviderException(string message, int? statusCode = null) : Exception(message)
{
    public int? StatusCode { get; } = statusCode;
}

/// <summary>Gọi API embeddings chuẩn OpenAI (OpenAI, Ollama /v1, các dịch vụ tương thích).</summary>
public sealed class OpenAiCompatibleEmbeddingClient(HttpClient http, RagEndpointOptions options) : IEmbeddingClient
{
    public string Model => options.Dimensions > 0 ? $"{options.Model}@{options.Dimensions}" : options.Model;

    public async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken ct = default)
    {
        if (inputs.Count == 0) return [];
        var body = new JsonObject
        {
            ["model"] = options.Model,
            ["input"] = new JsonArray(inputs.Select(text => (JsonNode?)JsonValue.Create(text)).ToArray())
        };
        if (options.Dimensions > 0) body["dimensions"] = options.Dimensions;

        var json = await OpenAiHttp.PostAsync(http, options, "embeddings", body, ct);
        var data = json["data"]?.AsArray() ?? throw new RagProviderException("API embeddings trả về thiếu trường data.");
        var vectors = new float[inputs.Count][];
        foreach (var item in data)
        {
            var index = item?["index"]?.GetValue<int>() ?? -1;
            var embedding = item?["embedding"]?.AsArray();
            if (index < 0 || index >= vectors.Length || embedding is null) throw new RagProviderException("API embeddings trả về dữ liệu không hợp lệ.");
            vectors[index] = embedding.Select(value => value!.GetValue<float>()).ToArray();
        }
        if (vectors.Any(vector => vector is null || vector.Length == 0)) throw new RagProviderException("API embeddings trả thiếu vector.");
        return vectors;
    }
}

/// <summary>Đọc ảnh bằng model vision qua API chat completions chuẩn OpenAI.</summary>
public sealed class OpenAiCompatibleVisionClient(HttpClient http, RagEndpointOptions options) : IImageTextReader
{
    private const string Instruction =
        "Bạn trích xuất nội dung từ ảnh trong tài liệu đặc tả phần mềm. Chép lại nguyên văn toàn bộ chữ nhìn thấy " +
        "(giữ ngôn ngữ gốc, giữ cấu trúc bảng bằng dấu |). Sau đó mô tả ngắn gọn sơ đồ, luồng, giao diện hoặc hình vẽ nếu có. " +
        "Chỉ trả về nội dung, không bình luận.";

    public bool IsConfigured => options.IsConfigured;

    public async Task<string> ReadAsync(byte[] image, string mimeType, CancellationToken ct = default)
    {
        var body = new JsonObject
        {
            ["model"] = options.Model,
            ["max_tokens"] = options.MaxTokens,
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = new JsonArray
                    {
                        new JsonObject { ["type"] = "text", ["text"] = Instruction },
                        new JsonObject
                        {
                            ["type"] = "image_url",
                            ["image_url"] = new JsonObject { ["url"] = $"data:{mimeType};base64,{Convert.ToBase64String(image)}" }
                        }
                    }
                }
            }
        };
        var json = await OpenAiHttp.PostAsync(http, options, "chat/completions", body, ct);
        return json["choices"]?[0]?["message"]?["content"]?.GetValue<string>()?.Trim() ?? string.Empty;
    }
}

internal static class OpenAiHttp
{
    public static async Task<JsonObject> PostAsync(HttpClient http, RagEndpointOptions options, string path, JsonObject body, CancellationToken ct)
    {
        // StringContent có Content-Length; JsonContent gửi chunked mà nhiều server "tương thích OpenAI"/proxy không đọc được.
        using var request = new HttpRequestMessage(HttpMethod.Post, options.Resolve(path))
        {
            Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrWhiteSpace(options.ApiKey)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds)));
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new RagProviderException($"API {options.Provider} không phản hồi sau {options.TimeoutSeconds} giây.");
        }
        catch (HttpRequestException ex)
        {
            throw new RagProviderException($"Không kết nối được API {options.Provider}: {ex.Message}");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                // Chỉ lấy message lỗi của nhà cung cấp, không log body request (có nội dung tài liệu).
                var detail = await response.Content.ReadAsStringAsync(ct);
                string? message = null;
                try { message = JsonNode.Parse(detail)?["error"]?["message"]?.GetValue<string>(); } catch (System.Text.Json.JsonException) { }
                throw new RagProviderException($"API {options.Provider} trả {(int)response.StatusCode}: {message ?? response.ReasonPhrase}", (int)response.StatusCode);
            }
            return await response.Content.ReadFromJsonAsync<JsonObject>(ct) ?? throw new RagProviderException($"API {options.Provider} trả về rỗng.");
        }
    }
}
