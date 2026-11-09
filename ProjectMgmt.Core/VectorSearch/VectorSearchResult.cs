namespace ProjectMgmt.Core.VectorSearch;

/// <summary>
/// Search hit representing a semantic chunk or document match.
/// </summary>
public sealed record VectorSearchResult
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string ContentSnippet { get; init; }
    public required string SourcePath { get; init; }
    public double Score { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}
