using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Domain.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace DeliveryIntelligence.Infrastructure.Repositories;

public class DataOpsRepository : IDataOpsRepository
{
    private readonly DeliveryIntelligenceDbContext _context;

    public DataOpsRepository(DeliveryIntelligenceDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AiDataset>> GetDatasetsAsync(CancellationToken cancellationToken = default) =>
        await _context.AiDatasets.AsNoTracking().OrderBy(d => d.Code).ToListAsync(cancellationToken);

    public Task<AiDataset?> GetDatasetAsync(Guid datasetId, CancellationToken cancellationToken = default) =>
        _context.AiDatasets.SingleOrDefaultAsync(d => d.Id == datasetId, cancellationToken);

    public Task<bool> DatasetCodeExistsAsync(string code, CancellationToken cancellationToken = default) =>
        _context.AiDatasets.AnyAsync(d => d.Code == code, cancellationToken);

    public async Task AddDatasetAsync(AiDataset dataset, CancellationToken cancellationToken = default) =>
        await _context.AiDatasets.AddAsync(dataset, cancellationToken);

    public async Task<IReadOnlyList<AiDatasetVersion>> GetVersionsAsync(Guid datasetId, CancellationToken cancellationToken = default) =>
        await _context.AiDatasetVersions.AsNoTracking()
            .Where(v => v.DatasetId == datasetId)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<AiDatasetVersion?> GetVersionAsync(Guid versionId, CancellationToken cancellationToken = default) =>
        _context.AiDatasetVersions.SingleOrDefaultAsync(v => v.Id == versionId, cancellationToken);

    public Task<bool> VersionTagExistsAsync(Guid datasetId, string versionTag, CancellationToken cancellationToken = default) =>
        _context.AiDatasetVersions.AnyAsync(v => v.DatasetId == datasetId && v.VersionTag == versionTag, cancellationToken);

    public async Task AddVersionAsync(AiDatasetVersion version, CancellationToken cancellationToken = default) =>
        await _context.AiDatasetVersions.AddAsync(version, cancellationToken);

    // Lọc theo DatasetVersionId trước (IX_AiDatasetSample_Version_Review / _Split), phân trang có chặn.
    public async Task<SamplePage> ListSamplesAsync(SampleFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var page = Math.Max(filter.Page, 1);
        var pageSize = Math.Clamp(filter.PageSize, 1, SampleFilter.MaxPageSize);
        var query = _context.AiDatasetSamples.AsNoTracking().Where(s => s.DatasetVersionId == filter.VersionId);
        if (!string.IsNullOrEmpty(filter.QualityStatus)) query = query.Where(s => s.QualityStatus == filter.QualityStatus);
        if (!string.IsNullOrEmpty(filter.ReviewStatus)) query = query.Where(s => s.ReviewStatus == filter.ReviewStatus);
        if (!string.IsNullOrEmpty(filter.SourceType)) query = query.Where(s => s.SourceType == filter.SourceType);
        if (!string.IsNullOrEmpty(filter.SplitType)) query = query.Where(s => s.SplitType == filter.SplitType);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(s => s.CreatedAt)
            .ThenBy(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new SamplePage(items, total);
    }

    public async Task<IReadOnlyList<AiDatasetSample>> GetVersionSamplesAsync(Guid versionId, CancellationToken cancellationToken = default) =>
        await _context.AiDatasetSamples
            .Where(s => s.DatasetVersionId == versionId)
            .OrderBy(s => s.CreatedAt)
            .ThenBy(s => s.Id)
            .ToListAsync(cancellationToken);

    public Task<AiDatasetSample?> GetSampleAsync(Guid sampleId, CancellationToken cancellationToken = default) =>
        _context.AiDatasetSamples.SingleOrDefaultAsync(s => s.Id == sampleId, cancellationToken);

    public Task<bool> SampleHashExistsAsync(Guid versionId, string contentHash, Guid? exceptSampleId, CancellationToken cancellationToken = default) =>
        _context.AiDatasetSamples.AnyAsync(
            s => s.DatasetVersionId == versionId && s.ContentHash == contentHash && (exceptSampleId == null || s.Id != exceptSampleId),
            cancellationToken);

    public async Task AddSampleAsync(AiDatasetSample sample, CancellationToken cancellationToken = default) =>
        await _context.AiDatasetSamples.AddAsync(sample, cancellationToken);

    public async Task<IReadOnlyList<AiDataCleaningRule>> GetRulesAsync(bool activeOnly, CancellationToken cancellationToken = default) =>
        await _context.AiDataCleaningRules
            .Where(r => !activeOnly || r.IsActive)
            .OrderBy(r => r.Code)
            .ToListAsync(cancellationToken);

    public Task<AiDataCleaningRule?> GetRuleAsync(Guid ruleId, CancellationToken cancellationToken = default) =>
        _context.AiDataCleaningRules.SingleOrDefaultAsync(r => r.Id == ruleId, cancellationToken);

    public Task<bool> RuleCodeExistsAsync(string code, CancellationToken cancellationToken = default) =>
        _context.AiDataCleaningRules.AnyAsync(r => r.Code == code, cancellationToken);

    public async Task AddRuleAsync(AiDataCleaningRule rule, CancellationToken cancellationToken = default) =>
        await _context.AiDataCleaningRules.AddAsync(rule, cancellationToken);

    public async Task<IReadOnlyList<AiDataQualityFlag>> GetFlagsAsync(
        IEnumerable<Guid> sampleIds,
        bool unresolvedOnly,
        CancellationToken cancellationToken = default)
    {
        var ids = sampleIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await _context.AiDataQualityFlags
            .Where(f => ids.Contains(f.SampleId) && (!unresolvedOnly || !f.IsResolved))
            .OrderBy(f => f.DetectedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<AiDataQualityFlag?> GetFlagAsync(Guid flagId, CancellationToken cancellationToken = default) =>
        _context.AiDataQualityFlags.SingleOrDefaultAsync(f => f.Id == flagId, cancellationToken);

    public async Task AddFlagAsync(AiDataQualityFlag flag, CancellationToken cancellationToken = default) =>
        await _context.AiDataQualityFlags.AddAsync(flag, cancellationToken);

    public void RemoveFlags(IEnumerable<AiDataQualityFlag> flags) => _context.AiDataQualityFlags.RemoveRange(flags);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => _context.SaveChangesAsync(cancellationToken);
}
