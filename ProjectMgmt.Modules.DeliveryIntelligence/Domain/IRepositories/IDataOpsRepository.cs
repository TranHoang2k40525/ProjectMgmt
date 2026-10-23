using DeliveryIntelligence.Domain.Entities;

namespace DeliveryIntelligence.Domain.IRepositories;

/// <summary>Dataset → Version → Sample, rule làm sạch và quality flag (DataOps, module C).</summary>
public interface IDataOpsRepository
{
    Task<IReadOnlyList<AiDataset>> GetDatasetsAsync(CancellationToken cancellationToken = default);

    Task<AiDataset?> GetDatasetAsync(Guid datasetId, CancellationToken cancellationToken = default);

    Task<bool> DatasetCodeExistsAsync(string code, CancellationToken cancellationToken = default);

    Task AddDatasetAsync(AiDataset dataset, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiDatasetVersion>> GetVersionsAsync(Guid datasetId, CancellationToken cancellationToken = default);

    Task<AiDatasetVersion?> GetVersionAsync(Guid versionId, CancellationToken cancellationToken = default);

    Task<bool> VersionTagExistsAsync(Guid datasetId, string versionTag, CancellationToken cancellationToken = default);

    Task AddVersionAsync(AiDatasetVersion version, CancellationToken cancellationToken = default);

    Task<SamplePage> ListSamplesAsync(SampleFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Toàn bộ sample của version (có tracking) — dùng cho chạy rule và freeze.</summary>
    Task<IReadOnlyList<AiDatasetSample>> GetVersionSamplesAsync(Guid versionId, CancellationToken cancellationToken = default);

    Task<AiDatasetSample?> GetSampleAsync(Guid sampleId, CancellationToken cancellationToken = default);

    Task<bool> SampleHashExistsAsync(Guid versionId, string contentHash, Guid? exceptSampleId, CancellationToken cancellationToken = default);

    Task AddSampleAsync(AiDatasetSample sample, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiDataCleaningRule>> GetRulesAsync(bool activeOnly, CancellationToken cancellationToken = default);

    Task<AiDataCleaningRule?> GetRuleAsync(Guid ruleId, CancellationToken cancellationToken = default);

    Task<bool> RuleCodeExistsAsync(string code, CancellationToken cancellationToken = default);

    Task AddRuleAsync(AiDataCleaningRule rule, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiDataQualityFlag>> GetFlagsAsync(IEnumerable<Guid> sampleIds, bool unresolvedOnly, CancellationToken cancellationToken = default);

    Task<AiDataQualityFlag?> GetFlagAsync(Guid flagId, CancellationToken cancellationToken = default);

    Task AddFlagAsync(AiDataQualityFlag flag, CancellationToken cancellationToken = default);

    void RemoveFlags(IEnumerable<AiDataQualityFlag> flags);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
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
