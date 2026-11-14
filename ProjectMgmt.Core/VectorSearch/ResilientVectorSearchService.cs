using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectMgmt.Core.Resilience;

namespace ProjectMgmt.Core.VectorSearch;

/// <summary>
/// Resilient vector search orchestrator.
/// Primary: Qdrant vector semantic similarity.
/// Fallback: MySQL keyword search / SpecHub document matcher.
/// Guarantee: Never throws on external container down; safely returns empty list if all backends fail.
/// </summary>
public sealed class ResilientVectorSearchService : IVectorSearchService
{
    private readonly IExternalVectorSearchProvider? _primaryProvider;
    private readonly ISpecKeywordFallbackProvider? _keywordFallbackProvider;
    private readonly CircuitBreakerPolicy _circuitBreaker;
    private readonly ILogger<ResilientVectorSearchService> _logger;
    private readonly bool _qdrantEnabled;

    public ResilientVectorSearchService(
        IExternalVectorSearchProvider? primaryProvider = null,
        ISpecKeywordFallbackProvider? keywordFallbackProvider = null,
        CircuitBreakerPolicy? circuitBreaker = null,
        bool qdrantEnabled = false,
        TimeSpan? timeout = null,
        ILogger<ResilientVectorSearchService>? logger = null)
    {
        _primaryProvider = primaryProvider;
        _keywordFallbackProvider = keywordFallbackProvider;
        _qdrantEnabled = qdrantEnabled && primaryProvider != null;
        _logger = logger ?? NullLogger<ResilientVectorSearchService>.Instance;
        _circuitBreaker = circuitBreaker ?? new CircuitBreakerPolicy(
            serviceName: "QdrantVectorSearch",
            failureThreshold: 3,
            breakDuration: TimeSpan.FromSeconds(60),
            executionTimeout: timeout ?? TimeSpan.FromMilliseconds(2500));
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchRelevantSpecsAsync(
        Guid projectId,
        string query,
        int topK = 5,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<VectorSearchResult>();
        }

        if (!_qdrantEnabled || _primaryProvider == null)
        {
            return await ExecuteKeywordFallbackAsync(projectId, query, topK, ct).ConfigureAwait(false);
        }

        return await _circuitBreaker.ExecuteWithFallbackAsync(
            primaryAction: async token =>
            {
                return await _primaryProvider.SearchAsync(projectId, query, topK, token).ConfigureAwait(false);
            },
            fallbackAction: async token =>
            {
                return await ExecuteKeywordFallbackAsync(projectId, query, topK, token).ConfigureAwait(false);
            },
            ct: ct,
            onFallback: (reason, ex) =>
            {
                _logger.LogWarning(ex, "[ResilientVectorSearch] {Reason} ProjectId={ProjectId}", reason, projectId);
            }).ConfigureAwait(false);
    }

    public async Task<bool> UpsertSpecChunkAsync(
        Guid projectId,
        string chunkId,
        string title,
        string content,
        string sourcePath,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken ct = default)
    {
        if (!_qdrantEnabled || _primaryProvider == null)
        {
            _logger.LogDebug("[ResilientVectorSearch] Qdrant disabled. Skipping vector upsert for chunk {ChunkId}", chunkId);
            return true;
        }

        return await _circuitBreaker.ExecuteWithFallbackAsync(
            primaryAction: async token =>
            {
                return await _primaryProvider.UpsertChunkAsync(projectId, chunkId, title, content, sourcePath, metadata, token).ConfigureAwait(false);
            },
            fallbackAction: token =>
            {
                _logger.LogWarning("[ResilientVectorSearch] Upsert skipped due to Qdrant unavailability. ChunkId={ChunkId}", chunkId);
                return Task.FromResult(false);
            },
            ct: ct,
            onFallback: (reason, ex) =>
            {
                _logger.LogWarning(ex, "[ResilientVectorSearch] {Reason} ChunkId={ChunkId}", reason, chunkId);
            }).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<VectorSearchResult>> ExecuteKeywordFallbackAsync(
        Guid projectId,
        string query,
        int topK,
        CancellationToken ct)
    {
        if (_keywordFallbackProvider == null)
        {
            _logger.LogInformation("[ResilientVectorSearch] No keyword fallback provider configured. Returning empty results.");
            return Array.Empty<VectorSearchResult>();
        }

        try
        {
            return await _keywordFallbackProvider.SearchKeywordAsync(projectId, query, topK, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ResilientVectorSearch] Keyword fallback search failed for ProjectId={ProjectId}", projectId);
            return Array.Empty<VectorSearchResult>();
        }
    }
}
