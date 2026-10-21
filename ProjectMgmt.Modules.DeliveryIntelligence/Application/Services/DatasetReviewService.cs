using System.Security.Cryptography;
using System.Text;
using DeliveryIntelligence.Application.Dto;
using DeliveryIntelligence.Application.IServices;
using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Domain.IRepositories;
using DeliveryIntelligence.Domain.UseCases;
using ProjectMgmt.BuildingBlocks.Results;

namespace DeliveryIntelligence.Application.Services;

public class DatasetReviewService : IDatasetReviewService
{
    private readonly IDataOpsRepository _data;
    private readonly TimeProvider _clock;

    public DatasetReviewService(IDataOpsRepository data, TimeProvider clock)
    {
        _data = data;
        _clock = clock;
    }

    public async Task<Result<DatasetSampleDto>> ApproveAsync(
        Guid sampleId,
        ApproveSampleDto request,
        Guid reviewerUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.QualityScore is < 0 or > 1)
        {
            return Result<DatasetSampleDto>.Failure(Error.Validation("SAMPLE_QUALITY_SCORE_INVALID", "Điểm chất lượng từ 0 đến 1."));
        }

        var (sample, blocked) = await EditableSampleAsync(sampleId, cancellationToken);
        if (blocked is not null)
        {
            return Result<DatasetSampleDto>.Failure(blocked);
        }

        if (sample!.QualityStatus != DatasetRules.Quality.Cleaned && sample.QualityStatus != DatasetRules.Quality.Approved)
        {
            return Result<DatasetSampleDto>.Failure(ReviewErrors.NotCleaned);
        }

        if (!request.PiiChecked)
        {
            return Result<DatasetSampleDto>.Failure(ReviewErrors.PiiNotChecked);
        }

        if (await HasBlockingFlagsAsync([sample.Id], cancellationToken))
        {
            return Result<DatasetSampleDto>.Failure(ReviewErrors.OpenBlockingFlags);
        }

        sample.QualityStatus = DatasetRules.Quality.Approved;
        sample.ReviewStatus = DatasetRules.Review.Approved;
        sample.IsPiiRedacted = true;
        sample.QualityScore = request.QualityScore is null ? null : Math.Round(request.QualityScore.Value, 3, MidpointRounding.AwayFromZero);
        Stamp(sample, reviewerUserId, request.Note);
        await _data.SaveChangesAsync(cancellationToken);
        return Result.Success(DatasetService.ToDto(sample));
    }

    public async Task<Result<DatasetSampleDto>> RejectAsync(
        Guid sampleId,
        RejectSampleDto request,
        Guid reviewerUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Note) || request.Note.Trim().Length > 1000)
        {
            return Result<DatasetSampleDto>.Failure(Error.Validation("SAMPLE_REJECT_NOTE_REQUIRED", "Nhập lý do từ chối (tối đa 1000 ký tự)."));
        }

        var (sample, blocked) = await EditableSampleAsync(sampleId, cancellationToken);
        if (blocked is not null)
        {
            return Result<DatasetSampleDto>.Failure(blocked);
        }

        sample!.QualityStatus = DatasetRules.Quality.Rejected;
        sample.ReviewStatus = DatasetRules.Review.Rejected;
        Stamp(sample, reviewerUserId, request.Note);
        await _data.SaveChangesAsync(cancellationToken);
        return Result.Success(DatasetService.ToDto(sample));
    }

    public async Task<Result<IReadOnlyList<QualityFlagDto>>> GetVersionFlagsAsync(
        Guid versionId,
        bool unresolvedOnly,
        CancellationToken cancellationToken = default)
    {
        if (await _data.GetVersionAsync(versionId, cancellationToken) is null)
        {
            return Result<IReadOnlyList<QualityFlagDto>>.Failure(DataOpsErrors.VersionNotFound);
        }

        var samples = await _data.GetVersionSamplesAsync(versionId, cancellationToken);
        var rules = (await _data.GetRulesAsync(activeOnly: false, cancellationToken)).ToDictionary(r => r.Id);
        IReadOnlyList<QualityFlagDto> result = (await _data.GetFlagsAsync(samples.Select(s => s.Id), unresolvedOnly, cancellationToken))
            .Select(flag => DataCleaningService.ToDto(flag, rules.GetValueOrDefault(flag.RuleId)))
            .ToList();
        return Result.Success(result);
    }

    public async Task<Result<DatasetVersionDto>> FreezeAsync(Guid versionId, FreezeVersionDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var version = await _data.GetVersionAsync(versionId, cancellationToken);
        if (version is null)
        {
            return Result<DatasetVersionDto>.Failure(DataOpsErrors.VersionNotFound);
        }

        if (version.IsFrozen)
        {
            return Result<DatasetVersionDto>.Failure(Error.Conflict("DATASET_VERSION_ALREADY_FROZEN", "Version đã được đóng băng."));
        }

        var samples = await _data.GetVersionSamplesAsync(versionId, cancellationToken);
        var pending = samples.Count(s => s.QualityStatus is not (DatasetRules.Quality.Approved or DatasetRules.Quality.Rejected));
        if (pending > 0)
        {
            return Result<DatasetVersionDto>.Failure(Error.Conflict("FREEZE_PENDING_SAMPLES", $"Còn {pending} mẫu chưa được duyệt hoặc từ chối."));
        }

        var approved = samples.Where(s => s.QualityStatus == DatasetRules.Quality.Approved).ToList();
        if (approved.Count == 0)
        {
            return Result<DatasetVersionDto>.Failure(Error.Conflict("FREEZE_NO_APPROVED_SAMPLES", "Version chưa có mẫu nào được duyệt."));
        }

        if (approved.Any(s => !s.IsPiiRedacted) || await HasBlockingFlagsAsync(approved.Select(s => s.Id), cancellationToken))
        {
            return Result<DatasetVersionDto>.Failure(Error.Conflict("FREEZE_QUALITY_CONDITIONS", "Mẫu đã duyệt còn PII chưa kiểm hoặc flag Error/PII chưa xử lý."));
        }

        version.SplitSeed = request.SplitSeed ?? RandomNumberGenerator.GetInt32(1, int.MaxValue);
        version.SampleCount = approved.Count;
        version.TrainCount = approved.Count(s => s.SplitType == "Train");
        version.ValidationCount = approved.Count(s => s.SplitType == "Validation");
        version.TestCount = approved.Count(s => s.SplitType == "Test");
        version.Checksum = ManifestChecksum(approved);
        version.IsFrozen = true;
        version.FrozenAt = _clock.GetUtcNow().UtcDateTime;
        await _data.SaveChangesAsync(cancellationToken);
        return Result.Success(DatasetService.ToDto(version));
    }

    /// <summary>
    /// SHA-256 của danh sách ContentHash mẫu đã duyệt sắp tăng dần (kèm split): cùng tập dữ liệu ⇒ cùng checksum.
    /// Checksum file JSONL khi export (S9) sẽ thay giá trị này.
    /// </summary>
    public static string ManifestChecksum(IEnumerable<AiDatasetSample> approved)
    {
        var manifest = string.Join('\n', approved
            .Select(s => $"{s.ContentHash}:{s.SplitType}")
            .OrderBy(line => line, StringComparer.Ordinal));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(manifest))).ToLowerInvariant();
    }

    private async Task<(AiDatasetSample? Sample, Error? Blocked)> EditableSampleAsync(Guid sampleId, CancellationToken cancellationToken)
    {
        var sample = await _data.GetSampleAsync(sampleId, cancellationToken);
        if (sample is null)
        {
            return (null, DataOpsErrors.SampleNotFound);
        }

        var version = await _data.GetVersionAsync(sample.DatasetVersionId, cancellationToken);
        return version is null || version.IsFrozen ? (sample, DataOpsErrors.VersionFrozen) : (sample, null);
    }

    /// <summary>Flag chặn duyệt: Severity Error hoặc thuộc rule PiiDetection mà chưa xử lý.</summary>
    private async Task<bool> HasBlockingFlagsAsync(IEnumerable<Guid> sampleIds, CancellationToken cancellationToken)
    {
        var rules = (await _data.GetRulesAsync(activeOnly: false, cancellationToken)).ToDictionary(r => r.Id);
        return (await _data.GetFlagsAsync(sampleIds, unresolvedOnly: true, cancellationToken))
            .Any(flag => flag.Severity == "Error" || rules.GetValueOrDefault(flag.RuleId)?.RuleType == "PiiDetection");
    }

    private void Stamp(AiDatasetSample sample, Guid reviewerUserId, string? note)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        sample.ReviewedBy = reviewerUserId;
        sample.ReviewedAt = now;
        sample.ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim()[..Math.Min(note.Trim().Length, 1000)];
        sample.UpdatedAt = now;
    }
}

public static class ReviewErrors
{
    public static readonly Error NotCleaned = Error.Conflict("SAMPLE_NOT_CLEANED", "Mẫu phải qua bước làm sạch (Cleaned) trước khi duyệt; hãy chạy rule.");
    public static readonly Error PiiNotChecked = Error.Validation("SAMPLE_PII_NOT_CHECKED", "Xác nhận đã kiểm và loại bỏ dữ liệu cá nhân trước khi duyệt.");
    public static readonly Error OpenBlockingFlags = Error.Conflict("SAMPLE_OPEN_FLAGS", "Mẫu còn flag Error hoặc PII chưa xử lý.");
}
