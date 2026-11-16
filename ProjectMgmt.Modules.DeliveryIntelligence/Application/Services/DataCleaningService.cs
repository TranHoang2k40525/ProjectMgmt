using DeliveryIntelligence.Application.Dto;
using DeliveryIntelligence.Application.IServices;
using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Domain.IRepositories;
using DeliveryIntelligence.Domain.UseCases;
using ProjectMgmt.BuildingBlocks.Results;

namespace DeliveryIntelligence.Application.Services;

public class DataCleaningService : IDataCleaningService
{
    private static readonly HashSet<string> AppliesToValues = new() { "All", "Breakdown", "Assignment" };
    private const string ErrorSeverity = "Error";

    private readonly IDataOpsRepository _data;
    private readonly TimeProvider _clock;

    public DataCleaningService(IDataOpsRepository data, TimeProvider clock)
    {
        _data = data;
        _clock = clock;
    }

    public async Task<IReadOnlyList<CleaningRuleDto>> ListRulesAsync() =>
        (await _data.GetRulesAsync(activeOnly: false)).Select(ToDto).ToList();

    public async Task<Result<CleaningRuleDto>> CreateRuleAsync(CreateCleaningRuleDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = request.Code.Trim().ToLowerInvariant();
        if (!DatasetRules.DatasetCodePattern().IsMatch(code))
        {
            return Result<CleaningRuleDto>.Failure(Error.Validation("CLEANING_RULE_CODE_INVALID", "Mã rule gồm chữ thường, số, '.', '-' (2–80 ký tự)."));
        }

        if (!CleaningRuleEvaluator.SupportedTypes.Contains(request.RuleType))
        {
            return Result<CleaningRuleDto>.Failure(CleaningErrors.TypeUnsupported);
        }

        var invalid = ValidateCommon(request.Name, request.Severity, request.Config, request.AppliesTo);
        if (invalid is not null)
        {
            return Result<CleaningRuleDto>.Failure(invalid);
        }

        if (await _data.RuleCodeExistsAsync(code))
        {
            return Result<CleaningRuleDto>.Failure(Error.Conflict("CLEANING_RULE_DUPLICATE", "Mã rule đã tồn tại."));
        }

        var rule = new AiDataCleaningRule
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = request.Name.Trim(),
            RuleType = request.RuleType,
            Severity = request.Severity,
            Config = NormalizeConfig(request.Config),
            AppliesTo = request.AppliesTo,
            IsActive = true,
            CreatedAt = _clock.GetUtcNow().UtcDateTime
        };
        await _data.AddRuleAsync(rule);
        await _data.SaveChangesAsync();
        return Result.Success(ToDto(rule));
    }

    public async Task<Result<CleaningRuleDto>> UpdateRuleAsync(Guid ruleId, UpdateCleaningRuleDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var invalid = ValidateCommon(request.Name, request.Severity, request.Config, request.AppliesTo);
        if (invalid is not null)
        {
            return Result<CleaningRuleDto>.Failure(invalid);
        }

        var rule = await _data.GetRuleAsync(ruleId);
        if (rule is null)
        {
            return Result<CleaningRuleDto>.Failure(CleaningErrors.RuleNotFound);
        }

        rule.Name = request.Name.Trim();
        rule.Severity = request.Severity;
        rule.Config = NormalizeConfig(request.Config);
        rule.AppliesTo = request.AppliesTo;
        rule.IsActive = request.IsActive;
        await _data.SaveChangesAsync();
        return Result.Success(ToDto(rule));
    }

    public async Task<Result<CleaningRunSummaryDto>> RunAsync(Guid versionId)
    {
        var version = await _data.GetVersionAsync(versionId);
        if (version is null)
        {
            return Result<CleaningRunSummaryDto>.Failure(DataOpsErrors.VersionNotFound);
        }

        if (version.IsFrozen)
        {
            return Result<CleaningRunSummaryDto>.Failure(DataOpsErrors.VersionFrozen);
        }

        var dataset = await _data.GetDatasetAsync(version.DatasetId);
        var rules = (await _data.GetRulesAsync(activeOnly: true))
            .Where(rule => CleaningRuleEvaluator.SupportedTypes.Contains(rule.RuleType)
                && (rule.AppliesTo == "All" || rule.AppliesTo == dataset?.TaskType))
            .ToList();
        var samples = await _data.GetVersionSamplesAsync(versionId);
        var instructionCounts = samples
            .GroupBy(sample => CleaningRuleEvaluator.NormalizeInstruction(sample.Instruction))
            .ToDictionary(group => group.Key, group => group.Count());

        // Chỉ thay flag chưa xử lý; flag đã xử lý được giữ lại. Phát hiện Warning/Info trùng flag đã xử lý không bị tạo lại
        // (người duyệt đã chấp nhận); rule Error là cổng cứng nên vẫn gắn lại — phải sửa nội dung mẫu.
        // Sửa nội dung mẫu xóa hết flag nên quyết định cũ không áp sai cho nội dung mới.
        var existing = await _data.GetFlagsAsync(samples.Select(s => s.Id), unresolvedOnly: false);
        _data.RemoveFlags(existing.Where(flag => !flag.IsResolved));
        var resolved = existing.Where(flag => flag.IsResolved)
            .Select(flag => (flag.SampleId, flag.RuleId, flag.FieldPath, flag.Message))
            .ToHashSet();

        var now = _clock.GetUtcNow().UtcDateTime;
        var summary = new CleaningRunSummaryDto { VersionId = versionId, SamplesChecked = samples.Count, RulesApplied = rules.Count };
        foreach (var sample in samples)
        {
            var hasError = false;
            var duplicates = instructionCounts[CleaningRuleEvaluator.NormalizeInstruction(sample.Instruction)] - 1;
            foreach (var rule in rules)
            {
                foreach (var finding in CleaningRuleEvaluator.Evaluate(rule, sample, duplicates))
                {
                    var message = Truncate(finding.Message, 1000)!;
                    var fieldPath = Truncate(finding.FieldPath, 200);
                    if (rule.Severity != ErrorSeverity && resolved.Contains((sample.Id, rule.Id, fieldPath, message)))
                    {
                        continue;
                    }

                    await _data.AddFlagAsync(new AiDataQualityFlag
                    {
                        Id = Guid.NewGuid(),
                        SampleId = sample.Id,
                        RuleId = rule.Id,
                        Severity = rule.Severity,
                        Message = message,
                        FieldPath = fieldPath,
                        DetectedAt = now
                    });
                    summary.FlagsCreated++;
                    summary.FlagsBySeverity[rule.Severity] = summary.FlagsBySeverity.GetValueOrDefault(rule.Severity) + 1;
                    hasError |= rule.Severity == ErrorSeverity;
                }
            }

            ApplyOutcome(sample, hasError, now, summary);
        }

        await _data.SaveChangesAsync();
        return Result.Success(summary);
    }

    public async Task<Result<IReadOnlyList<QualityFlagDto>>> GetSampleFlagsAsync(Guid sampleId)
    {
        if (await _data.GetSampleAsync(sampleId) is null)
        {
            return Result<IReadOnlyList<QualityFlagDto>>.Failure(DataOpsErrors.SampleNotFound);
        }

        var rules = (await _data.GetRulesAsync(activeOnly: false)).ToDictionary(r => r.Id);
        IReadOnlyList<QualityFlagDto> result = (await _data.GetFlagsAsync([sampleId], unresolvedOnly: false))
            .Select(flag => ToDto(flag, rules.GetValueOrDefault(flag.RuleId)))
            .ToList();
        return Result.Success(result);
    }

    public async Task<Result<QualityFlagDto>> ResolveFlagAsync(Guid flagId, Guid actorUserId)
    {
        var flag = await _data.GetFlagAsync(flagId);
        if (flag is null)
        {
            return Result<QualityFlagDto>.Failure(CleaningErrors.FlagNotFound);
        }

        var sample = await _data.GetSampleAsync(flag.SampleId);
        var version = sample is null ? null : await _data.GetVersionAsync(sample.DatasetVersionId);
        if (version is null || version.IsFrozen)
        {
            return Result<QualityFlagDto>.Failure(DataOpsErrors.VersionFrozen);
        }

        if (!flag.IsResolved)
        {
            flag.IsResolved = true;
            flag.ResolvedBy = actorUserId;
            flag.ResolvedAt = _clock.GetUtcNow().UtcDateTime;
            await _data.SaveChangesAsync();
        }

        return Result.Success(ToDto(flag, await _data.GetRuleAsync(flag.RuleId)));
    }

    /// <summary>Lỗi Error ⇒ Rejected (mẫu đã duyệt chuyển NeedsFix). Không lỗi: Raw/Rejected-do-rule ⇒ Cleaned; Approved giữ nguyên; mẫu bị người duyệt từ chối giữ Rejected.</summary>
    private static void ApplyOutcome(AiDatasetSample sample, bool hasError, DateTime now, CleaningRunSummaryDto summary)
    {
        var reviewerRejected = sample.ReviewStatus == DatasetRules.Review.Rejected;
        if (hasError)
        {
            if (sample.QualityStatus == DatasetRules.Quality.Approved)
            {
                sample.ReviewStatus = DatasetRules.Review.NeedsFix;
            }

            sample.QualityStatus = DatasetRules.Quality.Rejected;
            sample.UpdatedAt = now;
            summary.Rejected++;
            return;
        }

        if (!reviewerRejected && sample.QualityStatus is DatasetRules.Quality.Raw or DatasetRules.Quality.Rejected)
        {
            sample.QualityStatus = DatasetRules.Quality.Cleaned;
            sample.UpdatedAt = now;
            summary.Cleaned++;
        }
    }

    private static Error? ValidateCommon(string name, string severity, string? config, string appliesTo)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
        {
            return Error.Validation("CLEANING_RULE_NAME_INVALID", "Tên rule từ 1 đến 200 ký tự.");
        }

        if (!CleaningRuleEvaluator.Severities.Contains(severity))
        {
            return Error.Validation("CLEANING_RULE_SEVERITY_INVALID", "Severity phải là Info, Warning hoặc Error.");
        }

        if (!AppliesToValues.Contains(appliesTo))
        {
            return Error.Validation("CLEANING_RULE_APPLIES_TO_INVALID", "AppliesTo phải là All, Breakdown hoặc Assignment.");
        }

        return config is null || (config.Length <= 10_000 && SampleHasher.IsValidJson(config) && config.TrimStart().StartsWith('{'))
            ? null
            : Error.Validation("CLEANING_RULE_CONFIG_INVALID", "Config phải là JSON object (tối đa 10000 ký tự).");
    }

    private static string? NormalizeConfig(string? config) => config is null ? null : SampleHasher.CanonicalJson(config);

    private static string? Truncate(string? text, int max) => text is null || text.Length <= max ? text : text[..max];

    private static CleaningRuleDto ToDto(AiDataCleaningRule rule) => new()
    {
        Id = rule.Id,
        Code = rule.Code,
        Name = rule.Name,
        RuleType = rule.RuleType,
        Severity = rule.Severity,
        Config = rule.Config,
        AppliesTo = rule.AppliesTo,
        IsActive = rule.IsActive,
        IsSupported = CleaningRuleEvaluator.SupportedTypes.Contains(rule.RuleType)
    };

    internal static QualityFlagDto ToDto(AiDataQualityFlag flag, AiDataCleaningRule? rule) => new()
    {
        Id = flag.Id,
        SampleId = flag.SampleId,
        RuleId = flag.RuleId,
        RuleCode = rule?.Code ?? string.Empty,
        RuleType = rule?.RuleType ?? string.Empty,
        Severity = flag.Severity,
        Message = flag.Message,
        FieldPath = flag.FieldPath,
        IsResolved = flag.IsResolved,
        ResolvedBy = flag.ResolvedBy,
        ResolvedAt = flag.ResolvedAt,
        DetectedAt = flag.DetectedAt
    };
}

public static class CleaningErrors
{
    public static readonly Error RuleNotFound = Error.NotFound("CLEANING_RULE_NOT_FOUND", "Không tìm thấy rule làm sạch.");
    public static readonly Error TypeUnsupported = Error.Validation("CLEANING_RULE_TYPE_UNSUPPORTED", "RuleType phải là LengthCheck, SchemaValidation, Heuristic, Deduplication hoặc PiiDetection.");
    public static readonly Error FlagNotFound = Error.NotFound("QUALITY_FLAG_NOT_FOUND", "Không tìm thấy quality flag.");
}
