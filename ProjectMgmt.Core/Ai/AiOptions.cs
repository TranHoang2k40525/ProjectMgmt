namespace ProjectMgmt.Core.Ai;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public string Provider { get; set; } = "Gemini"; // "Gemini", "Claude", "OpenAi", "Local"
    public string DefaultModel { get; set; } = "gemini-2.5-flash";
    public int MaxTokens { get; set; } = 4096;
    public double Temperature { get; set; } = 0.2;
    public int TimeoutSeconds { get; set; } = 120;

    public GeminiProviderOptions Gemini { get; set; } = new();
    public ClaudeProviderOptions Claude { get; set; } = new();
    public OpenAiProviderOptions OpenAi { get; set; } = new();
    public LocalModelProviderOptions Local { get; set; } = new();

    public (string ApiKey, string Endpoint, string ModelName, int MaxTokens, double Temperature) GetActiveProviderSettings()
    {
        var normalizedProvider = Provider?.Trim().ToLowerInvariant() ?? string.Empty;
        (string ApiKey, string Endpoint, string ModelName, int MaxTokens, double Temperature) settings = normalizedProvider switch
        {
            "gemini" => (Gemini.ApiKey, Gemini.Endpoint, Gemini.ModelName, Gemini.MaxTokens ?? MaxTokens, Gemini.Temperature ?? Temperature),
            "claude" => (Claude.ApiKey, Claude.Endpoint, Claude.ModelName, Claude.MaxTokens ?? MaxTokens, Claude.Temperature ?? Temperature),
            "openai" => (OpenAi.ApiKey, OpenAi.Endpoint, OpenAi.ModelName, OpenAi.MaxTokens ?? MaxTokens, OpenAi.Temperature ?? Temperature),
            "local" or "ollama" => (Local.ApiKey ?? string.Empty, Local.Endpoint, Local.ModelName, Local.MaxTokens ?? MaxTokens, Local.Temperature ?? Temperature),
            _ => throw new NotSupportedException($"Nhà cung cấp AI '{Provider}' không được hỗ trợ. Các giá trị hợp lệ: Gemini, Claude, OpenAi, Local.")
        };

        if (normalizedProvider is "gemini" or "claude" or "openai")
        {
            if (string.IsNullOrWhiteSpace(settings.ApiKey) || settings.ApiKey.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Khóa API cho nhà cung cấp '{Provider}' chưa được thiết lập hoặc vẫn ở giá trị mẫu. Vui lòng cấu hình ApiKey trong appsettings.Local.json.");
            }
        }

        return settings;
    }
}

public sealed class GeminiProviderOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty; // lấy từ Ai:Gemini:Endpoint
    public string ModelName { get; set; } = "gemini-2.5-flash";
    public int? MaxTokens { get; set; }
    public double? Temperature { get; set; }
}

public sealed class ClaudeProviderOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty; // lấy từ Ai:Claude:Endpoint
    public string ModelName { get; set; } = "claude-3-7-sonnet-20250219";
    public int? MaxTokens { get; set; }
    public double? Temperature { get; set; }
}

public sealed class OpenAiProviderOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty; // lấy từ Ai:OpenAi:Endpoint
    public string ModelName { get; set; } = "gpt-4o";
    public string? OrganizationId { get; set; }
    public int? MaxTokens { get; set; }
    public double? Temperature { get; set; }
}

public sealed class LocalModelProviderOptions
{
    public string? ApiKey { get; set; }
    public string Endpoint { get; set; } = string.Empty; // lấy từ Ai:Local:Endpoint
    public string ModelName { get; set; } = "qwen2.5:32b";
    public int? MaxTokens { get; set; }
    public double? Temperature { get; set; }
}
