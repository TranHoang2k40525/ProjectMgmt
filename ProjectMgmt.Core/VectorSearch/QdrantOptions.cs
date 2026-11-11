namespace ProjectMgmt.Core.VectorSearch;

public sealed class QdrantOptions
{
    public const string SectionName = "Qdrant";

    public bool Enabled { get; set; }
    public string Host { get; set; } = "YOUR_QDRANT_HOST";
    public int Port { get; set; } = 6334;
    public string ApiKey { get; set; } = "YOUR_QDRANT_API_KEY";
    public bool UseHttps { get; set; }
    public string CollectionName { get; set; } = "projectmgmt_specs";
    public int TimeoutMs { get; set; } = 5000;
}
