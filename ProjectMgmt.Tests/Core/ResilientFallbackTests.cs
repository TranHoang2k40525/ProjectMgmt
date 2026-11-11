using ProjectMgmt.Core.Resilience;
using ProjectMgmt.Core.Staging;
using ProjectMgmt.Core.Tracing;
using ProjectMgmt.Core.VectorSearch;
using Xunit;

namespace ProjectMgmt.Tests.Core;

public class ResilientFallbackTests
{
    [Fact]
    public async Task ResilientStaging_WhenRedisFails_FallsBackToMemoryWithoutThrowing()
    {
        var memory = new MemoryStateStagingService();
        var failingRedis = new FailingExternalStagingProvider();
        var resilient = new ResilientStateStagingService(
            memoryFallback: memory,
            primaryProvider: failingRedis,
            redisEnabled: true,
            timeout: TimeSpan.FromMilliseconds(100));

        var sessionId = "session_fallback_test";
        var payload = "TestProposalContent";

        // Must succeed without throwing TimeoutException
        await resilient.SaveProposalAsync(sessionId, payload, TimeSpan.FromMinutes(5));

        var retrieved = await resilient.GetProposalAsync<string>(sessionId);
        Assert.Equal("TestProposalContent", retrieved);
    }

    [Fact]
    public async Task ResilientVectorSearch_WhenQdrantOffline_ReturnsKeywordFallback()
    {
        var failingQdrant = new FailingVectorSearchProvider();
        var keywordFallback = new MockKeywordFallbackProvider();
        var resilient = new ResilientVectorSearchService(
            primaryProvider: failingQdrant,
            keywordFallbackProvider: keywordFallback,
            qdrantEnabled: true,
            timeout: TimeSpan.FromMilliseconds(100));

        var results = await resilient.SearchRelevantSpecsAsync(Guid.NewGuid(), "Đăng nhập OTP", topK: 3);

        Assert.Single(results);
        Assert.Equal("Spec Đăng nhập", results[0].Title);
    }

    [Fact]
    public async Task ResilientTrace_WhenLangfuseFails_LogsWithoutThrowing()
    {
        var failingExporter = new FailingTraceExporter();
        var resilient = new ResilientTraceService(
            externalExporter: failingExporter,
            langfuseEnabled: true,
            timeout: TimeSpan.FromMilliseconds(50));

        // Tracing call must be fire-and-forget and safe
        await resilient.RecordGenerationAsync(
            traceId: "trace-123",
            modelName: "gemini-2.5-flash",
            prompt: "Prompt",
            completion: "Completion",
            promptTokens: 10,
            completionTokens: 20,
            durationMs: 150);

        Assert.True(true);
    }
}

public class FailingExternalStagingProvider : IExternalStagingProvider
{
    public string ProviderName => "MockFailingRedis";
    public Task<bool> IsAvailableAsync(CancellationToken ct) => Task.FromResult(false);
    public Task SetAsync(string key, string serializedValue, TimeSpan ttl, CancellationToken ct) =>
        throw new TimeoutException("Redis connection timed out.");
    public Task<string?> GetAsync(string key, CancellationToken ct) =>
        throw new TimeoutException("Redis connection timed out.");
    public Task<bool> RemoveAsync(string key, CancellationToken ct) =>
        throw new TimeoutException("Redis connection timed out.");
    public Task<bool> ExistsAsync(string key, CancellationToken ct) =>
        throw new TimeoutException("Redis connection timed out.");
}

public class FailingVectorSearchProvider : IExternalVectorSearchProvider
{
    public string ProviderName => "MockFailingQdrant";
    public Task<bool> IsAvailableAsync(CancellationToken ct) => Task.FromResult(false);
    public Task<IReadOnlyList<VectorSearchResult>> SearchAsync(Guid projectId, string query, int topK, CancellationToken ct) =>
        throw new HttpRequestException("Qdrant container connection refused.");
    public Task<bool> UpsertChunkAsync(Guid projectId, string chunkId, string title, string content, string sourcePath, IReadOnlyDictionary<string, string>? metadata, CancellationToken ct) =>
        throw new HttpRequestException("Qdrant container connection refused.");
}

public class MockKeywordFallbackProvider : ISpecKeywordFallbackProvider
{
    public Task<IReadOnlyList<VectorSearchResult>> SearchKeywordAsync(Guid projectId, string query, int topK, CancellationToken ct)
    {
        return Task.FromResult<IReadOnlyList<VectorSearchResult>>([
            new VectorSearchResult
            {
                Id = "spec-1",
                Title = "Spec Đăng nhập",
                ContentSnippet = "Quy trình đăng nhập OTP và bảo mật đa lớp",
                SourcePath = "docs/specs/login.md",
                Score = 0.75
            }
        ]);
    }
}

public class FailingTraceExporter : IExternalTraceExporter
{
    public string ExporterName => "MockFailingLangfuse";
    public Task<bool> IsAvailableAsync(CancellationToken ct) => Task.FromResult(false);
    public Task ExportTraceAsync(AiTraceSession session, CancellationToken ct) =>
        throw new HttpRequestException("Langfuse host unreachable.");
    public Task ExportSpanAsync(AiTraceSpan span, CancellationToken ct) =>
        throw new HttpRequestException("Langfuse host unreachable.");
}
