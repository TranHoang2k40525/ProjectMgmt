namespace ProjectMgmt.Core.VectorSearch;

/// <summary>
/// Primary semantic vector search client (e.g. Qdrant).
/// </summary>
public interface IExternalVectorSearchProvider
{
    string ProviderName { get; }
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        Guid projectId,
        string query,
        int topK,
        CancellationToken ct = default);
    Task<bool> UpsertChunkAsync(
        Guid projectId,
        string chunkId,
        string title,
        string content,
        string sourcePath,
        IReadOnlyDictionary<string, string>? metadata,
        CancellationToken ct = default);
}

/// <summary>
/// Fallback provider using MySQL text matching or SpecHub filesystem keywords.
/// Activated when Qdrant is disabled, offline, or circuit breaker is tripped.
/// </summary>
public interface ISpecKeywordFallbackProvider
{
    Task<IReadOnlyList<VectorSearchResult>> SearchKeywordAsync(
        Guid projectId,
        string query,
        int topK,
        CancellationToken ct = default);
}
