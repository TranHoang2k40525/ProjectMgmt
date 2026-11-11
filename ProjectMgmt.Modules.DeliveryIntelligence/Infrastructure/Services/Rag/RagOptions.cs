namespace DeliveryIntelligence.Infrastructure.Services.Rag;

/// <summary>
/// Cấu hình RAG (section "Rag" trong appsettings*.json). Mọi URL, model, key đều đọc từ cấu hình;
/// code không có giá trị mặc định cho URL nên thiếu cấu hình thì RAG tự tắt và quay về tìm theo từ khóa.
/// </summary>
public sealed class RagOptions
{
    public const string SectionName = "Rag";

    public bool Enabled { get; set; }

    /// <summary>API tạo vector (chuẩn OpenAI: POST {Endpoint}/embeddings).</summary>
    public RagEndpointOptions Embedding { get; set; } = new();

    /// <summary>API đọc ảnh / PDF scan (chuẩn OpenAI: POST {Endpoint}/chat/completions có image_url).</summary>
    public RagEndpointOptions Vision { get; set; } = new();

    /// <summary>Số ký tự tối đa mỗi đoạn và phần gối đầu giữa 2 đoạn liên tiếp.</summary>
    public int ChunkSize { get; set; } = 1200;
    public int ChunkOverlap { get; set; } = 200;
    public int MaxChunksPerDocument { get; set; } = 400;
    public int EmbeddingBatchSize { get; set; } = 32;

    public int TopK { get; set; } = 5;
    /// <summary>Bỏ các đoạn có độ tương đồng cosine thấp hơn ngưỡng này.</summary>
    public double MinScore { get; set; } = 0.2;

    /// <summary>Chu kỳ dịch vụ nền đối chiếu Spec Hub với chỉ mục; 0 = tắt, chỉ đánh chỉ mục khi gọi endpoint.</summary>
    public int IndexIntervalSeconds { get; set; } = 120;
    public long MaxFileBytes { get; set; } = 20 * 1024 * 1024;
    public long MaxImageBytes { get; set; } = 5 * 1024 * 1024;

    public bool IsSearchReady => Enabled && Embedding.IsConfigured;
    public bool IsVisionReady => Enabled && Vision.IsConfigured;
}

public sealed class RagEndpointOptions
{
    /// <summary>Tên nhà cung cấp, chỉ để ghi log (OpenAi, Ollama...).</summary>
    public string Provider { get; set; } = string.Empty;
    /// <summary>Gốc API, ví dụ ".../v1"; lấy từ appsettings.</summary>
    public string Endpoint { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    /// <summary>Không ghi vào file trong repo: đặt bằng user-secrets hoặc biến môi trường (Rag__Embedding__ApiKey).</summary>
    public string ApiKey { get; set; } = string.Empty;
    /// <summary>Số chiều vector yêu cầu (model hỗ trợ rút gọn như text-embedding-3-*); 0 = mặc định của model.</summary>
    public int Dimensions { get; set; }
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxTokens { get; set; } = 1500;

    public bool IsConfigured =>
        Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && !string.IsNullOrWhiteSpace(Model);

    /// <summary>{Endpoint}/{path}, giữ nguyên đường dẫn gốc (ví dụ /v1).</summary>
    public Uri Resolve(string path) => new(new Uri(Endpoint.TrimEnd('/') + "/"), path);
}
