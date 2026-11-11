namespace ProjectMgmt.Tests.Services;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProjectMgmt.Core.Ai;
using ProjectMgmt.Core.Security;
using ProjectMgmt.Core.Staging;
using ProjectMgmt.Core.Tracing;
using ProjectMgmt.Core.VectorSearch;
using ProjectMgmt.Solution.Services.Ai;
using ProjectMgmt.Solution.Services.Ai.Adapters;
using Xunit;

public class AiModelClientFallbackTests
{
    [Fact]
    public async Task MultiProviderAiModelClient_PlaceholderKey_ThrowsInvalidOperationException()
    {
        var options = Options.Create(new AiOptions
        {
            Provider = "Gemini",
            Gemini = new GeminiProviderOptions
            {
                ApiKey = "YOUR_GEMINI_API_KEY"
            }
        });

        var client = new MultiProviderAiModelClient(options, null!, NullLogger<MultiProviderAiModelClient>.Instance);
        var scope = new ProjectScopeContext(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var prompt = AiPrompt.FromUserMessage("Hello");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GenerateAsync(prompt, scope));

        Assert.Contains("chưa được thiết lập hoặc vẫn ở giá trị mẫu", ex.Message);
    }

    [Fact]
    public async Task MultiProviderAiModelClient_ScopeMismatch_ThrowsProjectScopeException()
    {
        var options = Options.Create(new AiOptions
        {
            Provider = "Gemini",
            Gemini = new GeminiProviderOptions
            {
                ApiKey = "valid-api-key"
            }
        });

        var client = new MultiProviderAiModelClient(options, null!, NullLogger<MultiProviderAiModelClient>.Instance);
        var emptyScope = ProjectScopeContext.Empty;
        var prompt = AiPrompt.FromUserMessage("Hello");

        await Assert.ThrowsAsync<ProjectScopeException>(() =>
            client.GenerateAsync(prompt, emptyScope));
    }

    [Fact]
    public async Task RedisStagingProvider_WhenDisabledOrPlaceholder_IsAvailableReturnsFalseCleanly()
    {
        var options = Options.Create(new RedisOptions
        {
            Enabled = false,
            Host = "YOUR_REDIS_HOST"
        });

        var provider = new StackExchangeRedisStagingProvider(options, NullLogger<StackExchangeRedisStagingProvider>.Instance);

        var isAvailable = await provider.IsAvailableAsync();
        Assert.False(isAvailable);

        var getResult = await provider.GetAsync("test_key");
        Assert.Null(getResult);

        var exists = await provider.ExistsAsync("test_key");
        Assert.False(exists);
    }

    [Fact]
    public async Task QdrantVectorSearchProvider_WhenDisabledOrPlaceholder_ReturnsEmptyWithoutThrowing()
    {
        var options = Options.Create(new QdrantOptions
        {
            Enabled = false,
            Host = "YOUR_QDRANT_HOST"
        });

        var provider = new QdrantVectorSearchProvider(options, null!, NullLogger<QdrantVectorSearchProvider>.Instance);

        var isAvailable = await provider.IsAvailableAsync();
        Assert.False(isAvailable);

        var results = await provider.SearchAsync(Guid.NewGuid(), "test query", 5);
        Assert.Empty(results);
    }

    [Fact]
    public async Task LangfuseTraceExporter_WhenDisabledOrPlaceholder_DoesNotThrow()
    {
        var options = Options.Create(new LangfuseOptions
        {
            Enabled = false,
            Host = "YOUR_LANGFUSE_HOST"
        });

        var exporter = new LangfuseTraceExporter(options, null!, NullLogger<LangfuseTraceExporter>.Instance);

        var isAvailable = await exporter.IsAvailableAsync();
        Assert.False(isAvailable);

        var session = new AiTraceSession
        {
            TraceId = "trace_01",
            SessionName = "Session 1",
            UserId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid()
        };

        var exception = await Record.ExceptionAsync(() => exporter.ExportTraceAsync(session));
        Assert.Null(exception);
    }
}
