using DeliveryIntelligence.Domain.Entities;

namespace DeliveryIntelligence.Domain.IRepositories;

/// <summary>Dataset → Version → Sample, rule làm sạch và quality flag (DataOps, module C).</summary>
public interface IDataOpsRepository
{
    Task<IReadOnlyList<AiDataset>> GetDatasetsAsync();

    Task<AiDataset?> GetDatasetAsync(Guid datasetId);

    Task<bool> DatasetCodeExistsAsync(string code);

    Task AddDatasetAsync(AiDataset dataset);

    Task<IReadOnlyList<AiDatasetVersion>> GetVersionsAsync(Guid datasetId);

    Task<AiDatasetVersion?> GetVersionAsync(Guid versionId);

    Task<bool> VersionTagExistsAsync(Guid datasetId, string versionTag);

    Task AddVersionAsync(AiDatasetVersion version);

    Task<SamplePage> ListSamplesAsync(SampleFilter filter);

    /// <summary>Toàn bộ sample của version (có tracking) — dùng cho chạy rule và freeze.</summary>
    Task<IReadOnlyList<AiDatasetSample>> GetVersionSamplesAsync(Guid versionId);

    Task<AiDatasetSample?> GetSampleAsync(Guid sampleId);

    Task<bool> SampleHashExistsAsync(Guid versionId, string contentHash, Guid? exceptSampleId);

    Task AddSampleAsync(AiDatasetSample sample);

    Task<IReadOnlyList<AiDataCleaningRule>> GetRulesAsync(bool activeOnly);

    Task<AiDataCleaningRule?> GetRuleAsync(Guid ruleId);

    Task<bool> RuleCodeExistsAsync(string code);

    Task AddRuleAsync(AiDataCleaningRule rule);

    Task<IReadOnlyList<AiDataQualityFlag>> GetFlagsAsync(IEnumerable<Guid> sampleIds, bool unresolvedOnly);

    Task<AiDataQualityFlag?> GetFlagAsync(Guid flagId);

    Task AddFlagAsync(AiDataQualityFlag flag);

    void RemoveFlags(IEnumerable<AiDataQualityFlag> flags);

    Task SaveChangesAsync();
}

public class SampleFilter
{
    public const int MaxPageSize = 100;

    public Guid VersionId { get; set; }
    public string? QualityStatus { get; set; }
    public string? ReviewStatus { get; set; }
    public string? SourceType { get; set; }
    public string? SplitType { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class SamplePage
{
    public SamplePage(IReadOnlyList<AiDatasetSample> items, int totalCount)
    {
        Items = items;
        TotalCount = totalCount;
    }

    public IReadOnlyList<AiDatasetSample> Items { get; }
    public int TotalCount { get; }
}
