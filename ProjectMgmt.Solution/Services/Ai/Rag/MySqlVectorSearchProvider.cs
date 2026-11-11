namespace ProjectMgmt.Solution.Services.Ai.Rag;

using System.Globalization;
using DeliveryIntelligence.Infrastructure.Services.Rag;
using Microsoft.Extensions.Options;
using ProjectMgmt.Core.VectorSearch;

/// <summary>
/// Tìm kiếm ngữ nghĩa trên bảng AiDocumentChunk (MySQL). Ghi chỉ mục do SpecDocumentIndexer làm,
/// nên UpsertChunkAsync không dùng ở đây.
/// </summary>
public sealed class MySqlVectorSearchProvider(RagSearchService search, IOptions<RagOptions> options) : IExternalVectorSearchProvider
{
    public string ProviderName => "MySqlVector";

    public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(options.Value.IsSearchReady);

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(Guid projectId, string query, int topK, CancellationToken ct = default)
    {
        var hits = await search.SearchAsync(projectId, query, topK, ct);
        return hits.Select(ToResult).ToList();
    }

    public Task<bool> UpsertChunkAsync(Guid projectId, string chunkId, string title, string content, string sourcePath,
        IReadOnlyDictionary<string, string>? metadata, CancellationToken ct = default) => Task.FromResult(false);

    public static VectorSearchResult ToResult(RagSearchHit hit)
    {
        var metadata = new Dictionary<string, string>
        {
            ["documentId"] = hit.DocumentId.ToString(),
            ["chunkId"] = hit.ChunkId.ToString()
        };
        if (hit.Heading is not null) metadata["heading"] = hit.Heading;
        if (hit.PageNumber is not null) metadata["page"] = hit.PageNumber.Value.ToString(CultureInfo.InvariantCulture);
        return new VectorSearchResult
        {
            Id = hit.ChunkId.ToString(),
            Title = hit.DocumentName,
            ContentSnippet = hit.Content,
            SourcePath = hit.DocumentName,
            Score = hit.Score,
            Metadata = metadata
        };
    }
}
