using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Domain.IRepositories;

namespace ProjectMgmt.Tests.Support;

/// <summary>Fake IDataOpsRepository trong bộ nhớ, cùng thứ tự/lọc/phân trang với DataOpsRepository.</summary>
public class InMemoryDataOpsRepository : IDataOpsRepository
{
    public List<AiDataset> Datasets { get; } = [];
    public List<AiDatasetVersion> Versions { get; } = [];
    public List<AiDatasetSample> Samples { get; } = [];
    public List<AiDataCleaningRule> Rules { get; } = [];
    public List<AiDataQualityFlag> Flags { get; } = [];
    public int SaveCount { get; private set; }

    public Task<IReadOnlyList<AiDataset>> GetDatasetsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AiDataset>>(Datasets.OrderBy(d => d.Code).ToList());

    public Task<AiDataset?> GetDatasetAsync(Guid datasetId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Datasets.SingleOrDefault(d => d.Id == datasetId));

    public Task<bool> DatasetCodeExistsAsync(string code, CancellationToken cancellationToken = default) =>
        Task.FromResult(Datasets.Any(d => d.Code == code));

    public Task AddDatasetAsync(AiDataset dataset, CancellationToken cancellationToken = default)
    {
        Datasets.Add(dataset);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AiDatasetVersion>> GetVersionsAsync(Guid datasetId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AiDatasetVersion>>(Versions.Where(v => v.DatasetId == datasetId).OrderByDescending(v => v.CreatedAt).ToList());

    public Task<AiDatasetVersion?> GetVersionAsync(Guid versionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Versions.SingleOrDefault(v => v.Id == versionId));

    public Task<bool> VersionTagExistsAsync(Guid datasetId, string versionTag, CancellationToken cancellationToken = default) =>
        Task.FromResult(Versions.Any(v => v.DatasetId == datasetId && v.VersionTag == versionTag));

    public Task AddVersionAsync(AiDatasetVersion version, CancellationToken cancellationToken = default)
    {
        Versions.Add(version);
        return Task.CompletedTask;
    }

    public Task<SamplePage> ListSamplesAsync(SampleFilter filter, CancellationToken cancellationToken = default)
    {
        var query = Samples.Where(s => s.DatasetVersionId == filter.VersionId);
        if (!string.IsNullOrEmpty(filter.QualityStatus)) query = query.Where(s => s.QualityStatus == filter.QualityStatus);
        if (!string.IsNullOrEmpty(filter.ReviewStatus)) query = query.Where(s => s.ReviewStatus == filter.ReviewStatus);
        if (!string.IsNullOrEmpty(filter.SourceType)) query = query.Where(s => s.SourceType == filter.SourceType);
        if (!string.IsNullOrEmpty(filter.SplitType)) query = query.Where(s => s.SplitType == filter.SplitType);
        var matched = query.OrderBy(s => s.CreatedAt).ThenBy(s => s.Id).ToList();
        var pageSize = Math.Clamp(filter.PageSize, 1, SampleFilter.MaxPageSize);
        IReadOnlyList<AiDatasetSample> items = matched.Skip((Math.Max(filter.Page, 1) - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult(new SamplePage(items, matched.Count));
    }

    public Task<IReadOnlyList<AiDatasetSample>> GetVersionSamplesAsync(Guid versionId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AiDatasetSample>>(Samples.Where(s => s.DatasetVersionId == versionId).OrderBy(s => s.CreatedAt).ThenBy(s => s.Id).ToList());

    public Task<AiDatasetSample?> GetSampleAsync(Guid sampleId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Samples.SingleOrDefault(s => s.Id == sampleId));

    public Task<bool> SampleHashExistsAsync(Guid versionId, string contentHash, Guid? exceptSampleId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Samples.Any(s => s.DatasetVersionId == versionId && s.ContentHash == contentHash && s.Id != exceptSampleId));

    public Task AddSampleAsync(AiDatasetSample sample, CancellationToken cancellationToken = default)
    {
        Samples.Add(sample);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AiDataCleaningRule>> GetRulesAsync(bool activeOnly, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AiDataCleaningRule>>(Rules.Where(r => !activeOnly || r.IsActive).OrderBy(r => r.Code).ToList());

    public Task<AiDataCleaningRule?> GetRuleAsync(Guid ruleId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Rules.SingleOrDefault(r => r.Id == ruleId));

    public Task<bool> RuleCodeExistsAsync(string code, CancellationToken cancellationToken = default) =>
        Task.FromResult(Rules.Any(r => r.Code == code));

    public Task AddRuleAsync(AiDataCleaningRule rule, CancellationToken cancellationToken = default)
    {
        Rules.Add(rule);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AiDataQualityFlag>> GetFlagsAsync(IEnumerable<Guid> sampleIds, bool unresolvedOnly, CancellationToken cancellationToken = default)
    {
        var ids = sampleIds.ToHashSet();
        return Task.FromResult<IReadOnlyList<AiDataQualityFlag>>(Flags.Where(f => ids.Contains(f.SampleId) && (!unresolvedOnly || !f.IsResolved)).OrderBy(f => f.DetectedAt).ToList());
    }

    public Task<AiDataQualityFlag?> GetFlagAsync(Guid flagId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Flags.SingleOrDefault(f => f.Id == flagId));

    public Task AddFlagAsync(AiDataQualityFlag flag, CancellationToken cancellationToken = default)
    {
        Flags.Add(flag);
        return Task.CompletedTask;
    }

    public void RemoveFlags(IEnumerable<AiDataQualityFlag> flags)
    {
        foreach (var flag in flags.ToList())
        {
            Flags.Remove(flag);
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
