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

    public async Task<IReadOnlyList<AiDataset>> GetDatasetsAsync() =>
        await _context.AiDatasets.AsNoTracking().OrderBy(d => d.Code).ToListAsync();

    public Task<AiDataset?> GetDatasetAsync(Guid datasetId) =>
        _context.AiDatasets.SingleOrDefaultAsync(d => d.Id == datasetId);

    public Task<bool> DatasetCodeExistsAsync(string code) =>
        _context.AiDatasets.AnyAsync(d => d.Code == code);

    public async Task AddDatasetAsync(AiDataset dataset) =>
        await _context.AiDatasets.AddAsync(dataset);

    public async Task<IReadOnlyList<AiDatasetVersion>> GetVersionsAsync(Guid datasetId) =>
        await _context.AiDatasetVersions.AsNoTracking()
            .Where(v => v.DatasetId == datasetId)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();

    public Task<AiDatasetVersion?> GetVersionAsync(Guid versionId) =>
        _context.AiDatasetVersions.SingleOrDefaultAsync(v => v.Id == versionId);

    public Task<bool> VersionTagExistsAsync(Guid datasetId, string versionTag) =>
        _context.AiDatasetVersions.AnyAsync(v => v.DatasetId == datasetId && v.VersionTag == versionTag);

    public async Task AddVersionAsync(AiDatasetVersion version) =>
        await _context.AiDatasetVersions.AddAsync(version);

    // Lọc theo DatasetVersionId trước (IX_AiDatasetSample_Version_Review / _Split), phân trang có chặn.
    public async Task<SamplePage> ListSamplesAsync(SampleFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var page = Math.Max(filter.Page, 1);
        var pageSize = Math.Clamp(filter.PageSize, 1, SampleFilter.MaxPageSize);
        var query = _context.AiDatasetSamples.AsNoTracking().Where(s => s.DatasetVersionId == filter.VersionId);
        if (!string.IsNullOrEmpty(filter.QualityStatus)) query = query.Where(s => s.QualityStatus == filter.QualityStatus);
        if (!string.IsNullOrEmpty(filter.ReviewStatus)) query = query.Where(s => s.ReviewStatus == filter.ReviewStatus);
        if (!string.IsNullOrEmpty(filter.SourceType)) query = query.Where(s => s.SourceType == filter.SourceType);
        if (!string.IsNullOrEmpty(filter.SplitType)) query = query.Where(s => s.SplitType == filter.SplitType);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(s => s.CreatedAt)
            .ThenBy(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return new SamplePage(items, total);
    }

    public async Task<IReadOnlyList<AiDatasetSample>> GetVersionSamplesAsync(Guid versionId) =>
        await _context.AiDatasetSamples
            .Where(s => s.DatasetVersionId == versionId)
            .OrderBy(s => s.CreatedAt)
            .ThenBy(s => s.Id)
            .ToListAsync();

    public Task<AiDatasetSample?> GetSampleAsync(Guid sampleId) =>
        _context.AiDatasetSamples.SingleOrDefaultAsync(s => s.Id == sampleId);

    public Task<bool> SampleHashExistsAsync(Guid versionId, string contentHash, Guid? exceptSampleId) =>
        _context.AiDatasetSamples.AnyAsync(
            s => s.DatasetVersionId == versionId && s.ContentHash == contentHash && (exceptSampleId == null || s.Id != exceptSampleId));

    public async Task AddSampleAsync(AiDatasetSample sample) =>
        await _context.AiDatasetSamples.AddAsync(sample);

    public async Task<IReadOnlyList<AiDataCleaningRule>> GetRulesAsync(bool activeOnly) =>
        await _context.AiDataCleaningRules
            .Where(r => !activeOnly || r.IsActive)
            .OrderBy(r => r.Code)
            .ToListAsync();

    public Task<AiDataCleaningRule?> GetRuleAsync(Guid ruleId) =>
        _context.AiDataCleaningRules.SingleOrDefaultAsync(r => r.Id == ruleId);

    public Task<bool> RuleCodeExistsAsync(string code) =>
        _context.AiDataCleaningRules.AnyAsync(r => r.Code == code);

    public async Task AddRuleAsync(AiDataCleaningRule rule) =>
        await _context.AiDataCleaningRules.AddAsync(rule);

    public async Task<IReadOnlyList<AiDataQualityFlag>> GetFlagsAsync(
        IEnumerable<Guid> sampleIds,
        bool unresolvedOnly)
    {
        var ids = sampleIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await _context.AiDataQualityFlags
            .Where(f => ids.Contains(f.SampleId) && (!unresolvedOnly || !f.IsResolved))
            .OrderBy(f => f.DetectedAt)
            .ToListAsync();
    }

    public Task<AiDataQualityFlag?> GetFlagAsync(Guid flagId) =>
        _context.AiDataQualityFlags.SingleOrDefaultAsync(f => f.Id == flagId);

    public async Task AddFlagAsync(AiDataQualityFlag flag) =>
        await _context.AiDataQualityFlags.AddAsync(flag);

    public void RemoveFlags(IEnumerable<AiDataQualityFlag> flags) => _context.AiDataQualityFlags.RemoveRange(flags);

    public Task SaveChangesAsync() => _context.SaveChangesAsync();
}
