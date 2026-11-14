using ProjectMgmt.Core.Ai;
using Xunit;

namespace ProjectMgmt.Tests.Core;

public class AiOptionsTests
{
    [Fact]
    public void GetActiveProviderSettings_Gemini_WithValidKey_ReturnsGeminiSettings()
    {
        var options = new AiOptions
        {
            Provider = "Gemini",
            Gemini = new GeminiProviderOptions
            {
                ApiKey = "valid_gemini_key_12345",
                Endpoint = "https://generativelanguage.googleapis.com",
                ModelName = "gemini-2.5-flash",
                MaxTokens = 8192,
                Temperature = 0.4
            }
        };

        var (apiKey, endpoint, modelName, maxTokens, temperature) = options.GetActiveProviderSettings();

        Assert.Equal("valid_gemini_key_12345", apiKey);
        Assert.Equal("https://generativelanguage.googleapis.com", endpoint);
        Assert.Equal("gemini-2.5-flash", modelName);
        Assert.Equal(8192, maxTokens);
        Assert.Equal(0.4, temperature);
    }

    [Fact]
    public void GetActiveProviderSettings_Claude_WithValidKey_ReturnsClaudeSettings()
    {
        var options = new AiOptions
        {
            Provider = "Claude",
            Claude = new ClaudeProviderOptions
            {
                ApiKey = "valid_claude_key_mock",
                Endpoint = "https://api.anthropic.com",
                ModelName = "claude-3-7-sonnet-20250219"
            }
        };

        var (apiKey, endpoint, modelName, maxTokens, _) = options.GetActiveProviderSettings();

        Assert.Equal("valid_claude_key_mock", apiKey);
        Assert.Equal("https://api.anthropic.com", endpoint);
        Assert.Equal("claude-3-7-sonnet-20250219", modelName);
        Assert.Equal(options.MaxTokens, maxTokens); // Fallback to root MaxTokens
    }

    [Fact]
    public void GetActiveProviderSettings_OpenAi_WithValidKey_ReturnsOpenAiSettings()
    {
        var options = new AiOptions
        {
            Provider = "OpenAi",
            OpenAi = new OpenAiProviderOptions
            {
                ApiKey = "valid_openai_key_mock",
                Endpoint = "https://api.openai.com",
                ModelName = "gpt-4o"
            }
        };

        var (apiKey, endpoint, modelName, _, _) = options.GetActiveProviderSettings();

        Assert.Equal("valid_openai_key_mock", apiKey);
        Assert.Equal("https://api.openai.com", endpoint);
        Assert.Equal("gpt-4o", modelName);
    }

    [Fact]
    public void GetActiveProviderSettings_Local_AllowsEmptyApiKey()
    {
        var options = new AiOptions
        {
            Provider = "Local",
            Local = new LocalModelProviderOptions
            {
                ApiKey = string.Empty,
                Endpoint = "http://localhost:11434",
                ModelName = "qwen2.5:32b"
            }
        };

        var (apiKey, endpoint, modelName, _, _) = options.GetActiveProviderSettings();

        Assert.Equal(string.Empty, apiKey);
        Assert.Equal("http://localhost:11434", endpoint);
        Assert.Equal("qwen2.5:32b", modelName);
    }

    [Theory]
    [InlineData("Gemini", "YOUR_GEMINI_API_KEY")]
    [InlineData("Claude", "YOUR_CLAUDE_API_KEY")]
    [InlineData("OpenAi", "YOUR_OPENAI_API_KEY")]
    [InlineData("Gemini", "")]
    [InlineData("Claude", "   ")]
    public void GetActiveProviderSettings_CommercialProviderWithPlaceholderOrEmptyKey_ThrowsInvalidOperationException(
        string provider, string placeholderKey)
    {
        var options = new AiOptions
        {
            Provider = provider
        };

        if (provider == "Gemini") options.Gemini.ApiKey = placeholderKey;
        if (provider == "Claude") options.Claude.ApiKey = placeholderKey;
        if (provider == "OpenAi") options.OpenAi.ApiKey = placeholderKey;

        var ex = Assert.Throws<InvalidOperationException>(() => options.GetActiveProviderSettings());

        Assert.Contains("appsettings.Local.json", ex.Message);
    }

    [Fact]
    public void GetActiveProviderSettings_UnsupportedProvider_ThrowsNotSupportedException()
    {
        var options = new AiOptions { Provider = "MistralAi" };

        var ex = Assert.Throws<NotSupportedException>(() => options.GetActiveProviderSettings());

        Assert.Contains("không được hỗ trợ", ex.Message);
    }
}
