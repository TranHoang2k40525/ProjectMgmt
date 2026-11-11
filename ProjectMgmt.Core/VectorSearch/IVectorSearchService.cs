namespace ProjectMgmt.Core.VectorSearch;

/// <summary>
/// Contract for semantic vector search across project documentation, SpecHub specs, and backlog stories.
/// </summary>
public interface IVectorSearchService
{
    /// <summary>
    /// Searches for relevant specifications within a project.
    /// </summary>
    Task<IReadOnlyList<VectorSearchResult>> SearchRelevantSpecsAsync(
        Guid projectId,
        string query,
        int topK = 5,
        CancellationToken ct = default);

    /// <summary>
    /// Indexes or updates a document specification chunk in vector storage.
    /// </summary>
    Task<bool> UpsertSpecChunkAsync(
        Guid projectId,
        string chunkId,
        string title,
        string content,
        string sourcePath,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken ct = default);
}
