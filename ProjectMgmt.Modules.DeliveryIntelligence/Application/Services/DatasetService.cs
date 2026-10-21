using System.Text.Json;
using DeliveryIntelligence.Application.Dto;
using DeliveryIntelligence.Application.IServices;
using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Domain.IRepositories;
using DeliveryIntelligence.Domain.UseCases;
using ProjectMgmt.BuildingBlocks.Results;

namespace DeliveryIntelligence.Application.Services;

public class DatasetService : IDatasetService
{
    public const int InstructionMaxLength = 100_000;
    public const int JsonMaxLength = 1_000_000;
    private readonly IDataOpsRepository _data;
    private readonly TimeProvider _clock;

    public DatasetService(IDataOpsRepository data, TimeProvider clock)
    {
        _data = data;
        _clock = clock;
    }

    public async Task<IReadOnlyList<DatasetDto>> ListDatasetsAsync(CancellationToken cancellationToken = default) =>
        (await _data.GetDatasetsAsync(cancellationToken)).Select(ToDto).ToList();

    public async Task<Result<DatasetDto>> CreateDatasetAsync(CreateDatasetDto request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = request.Code.Trim().ToLowerInvariant();
        var name = request.Name.Trim();
        if (!DatasetRules.DatasetCodePattern().IsMatch(code))
        {
            return Result<DatasetDto>.Failure(DataOpsErrors.DatasetCodeInvalid);
        }

        if (name.Length is < 2 or > 200)
        {
            return Result<DatasetDto>.Failure(Error.Validation("DATASET_NAME_INVALID", "Tên dataset từ 2 đến 200 ký tự."));
        }

        if (!DatasetRules.TaskTypes.Contains(request.TaskType))
        {
            return Result<DatasetDto>.Failure(Error.Validation("DATASET_TASK_TYPE_INVALID", "TaskType phải là Breakdown hoặc Assignment."));
        }

        var languages = request.Languages.Select(l => l.Trim().ToLowerInvariant()).Where(l => l.Length is >= 2 and <= 10).Distinct().ToList();
        if (await _data.DatasetCodeExistsAsync(code, cancellationToken))
        {
            return Result<DatasetDto>.Failure(DataOpsErrors.DatasetCodeDuplicate);
        }

        var dataset = new AiDataset
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            TaskType = request.TaskType,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Languages = JsonSerializer.Serialize(languages.Count == 0 ? ["vi"] : languages),
            CreatedBy = actorUserId,
            CreatedAt = Now()
        };
        await _data.AddDatasetAsync(dataset, cancellationToken);
        await _data.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(dataset));
    }

    public async Task<Result<IReadOnlyList<DatasetVersionDto>>> ListVersionsAsync(Guid datasetId, CancellationToken cancellationToken = default)
    {
        if (await _data.GetDatasetAsync(datasetId, cancellationToken) is null)
        {
            return Result<IReadOnlyList<DatasetVersionDto>>.Failure(DataOpsErrors.DatasetNotFound);
        }

        IReadOnlyList<DatasetVersionDto> result = (await _data.GetVersionsAsync(datasetId, cancellationToken)).Select(ToDto).ToList();
        return Result.Success(result);
    }

    public async Task<Result<DatasetVersionDto>> CreateVersionAsync(Guid datasetId, CreateDatasetVersionDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await _data.GetDatasetAsync(datasetId, cancellationToken) is null)
        {
            return Result<DatasetVersionDto>.Failure(DataOpsErrors.DatasetNotFound);
        }

        var tag = request.VersionTag.Trim();
        if (!DatasetRules.VersionTagPattern().IsMatch(tag))
        {
            return Result<DatasetVersionDto>.Failure(Error.Validation("DATASET_VERSION_TAG_INVALID", "VersionTag có dạng v1.0 hoặc v1.0.0."));
        }

        if (await _data.VersionTagExistsAsync(datasetId, tag, cancellationToken))
        {
            return Result<DatasetVersionDto>.Failure(Error.Conflict("DATASET_VERSION_DUPLICATE", "Dataset đã có version này."));
        }

        var version = new AiDatasetVersion
        {
            Id = Guid.NewGuid(),
            DatasetId = datasetId,
            VersionTag = tag,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedAt = Now()
        };
        await _data.AddVersionAsync(version, cancellationToken);
        await _data.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(version));
    }

    public async Task<Result<DatasetVersionDto>> GetVersionAsync(Guid versionId, CancellationToken cancellationToken = default)
    {
        var version = await _data.GetVersionAsync(versionId, cancellationToken);
        return version is null ? Result<DatasetVersionDto>.Failure(DataOpsErrors.VersionNotFound) : Result.Success(ToDto(version));
    }

    public async Task<Result<PagedResultDto<DatasetSampleDto>>> ListSamplesAsync(Guid versionId, SampleQueryDto query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Page < 1 || query.PageSize is < 1 or > SampleFilter.MaxPageSize)
        {
            return Result<PagedResultDto<DatasetSampleDto>>.Failure(Error.Validation("SAMPLE_PAGE_INVALID", $"Trang từ 1, mỗi trang 1–{SampleFilter.MaxPageSize} mẫu."));
        }

        if (await _data.GetVersionAsync(versionId, cancellationToken) is null)
        {
            return Result<PagedResultDto<DatasetSampleDto>>.Failure(DataOpsErrors.VersionNotFound);
        }

        var page = await _data.ListSamplesAsync(new SampleFilter
        {
            VersionId = versionId,
            QualityStatus = query.QualityStatus,
            ReviewStatus = query.ReviewStatus,
            SourceType = query.SourceType,
            SplitType = query.SplitType,
            Page = query.Page,
            PageSize = query.PageSize
        }, cancellationToken);
        return Result.Success(new PagedResultDto<DatasetSampleDto>
        {
            Items = page.Items.Select(ToDto).ToList(),
            TotalCount = page.TotalCount,
            Page = query.Page,
            PageSize = query.PageSize
        });
    }

    public async Task<Result<DatasetSampleDto>> GetSampleAsync(Guid sampleId, CancellationToken cancellationToken = default)
    {
        var sample = await _data.GetSampleAsync(sampleId, cancellationToken);
        return sample is null ? Result<DatasetSampleDto>.Failure(DataOpsErrors.SampleNotFound) : Result.Success(ToDto(sample));
    }

    public async Task<Result<DatasetSampleDto>> AddSampleAsync(Guid versionId, SaveDatasetSampleDto request, CancellationToken cancellationToken = default)
    {
        var invalid = Validate(request);
        if (invalid is not null)
        {
            return Result<DatasetSampleDto>.Failure(invalid);
        }

        var version = await _data.GetVersionAsync(versionId, cancellationToken);
        if (version is null)
        {
            return Result<DatasetSampleDto>.Failure(DataOpsErrors.VersionNotFound);
        }

        if (version.IsFrozen)
        {
            return Result<DatasetSampleDto>.Failure(DataOpsErrors.VersionFrozen);
        }

        var hash = SampleHasher.Compute(request.Instruction, request.InputJson, request.OutputJson);
        if (await _data.SampleHashExistsAsync(versionId, hash, null, cancellationToken))
        {
            return Result<DatasetSampleDto>.Failure(DataOpsErrors.SampleDuplicate);
        }

        var sample = new AiDatasetSample
        {
            Id = Guid.NewGuid(),
            DatasetVersionId = versionId,
            CreatedAt = Now()
        };
        Apply(sample, request, hash);
        await _data.AddSampleAsync(sample, cancellationToken);
        version.SampleCount++;
        await _data.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(sample));
    }

    public async Task<Result<DatasetSampleDto>> UpdateSampleAsync(Guid sampleId, SaveDatasetSampleDto request, CancellationToken cancellationToken = default)
    {
        var invalid = Validate(request);
        if (invalid is not null)
        {
            return Result<DatasetSampleDto>.Failure(invalid);
        }

        var sample = await _data.GetSampleAsync(sampleId, cancellationToken);
        if (sample is null)
        {
            return Result<DatasetSampleDto>.Failure(DataOpsErrors.SampleNotFound);
        }

        var version = await _data.GetVersionAsync(sample.DatasetVersionId, cancellationToken);
        if (version is null || version.IsFrozen)
        {
            return Result<DatasetSampleDto>.Failure(DataOpsErrors.VersionFrozen);
        }

        var hash = SampleHasher.Compute(request.Instruction, request.InputJson, request.OutputJson);
        if (await _data.SampleHashExistsAsync(sample.DatasetVersionId, hash, sample.Id, cancellationToken))
        {
            return Result<DatasetSampleDto>.Failure(DataOpsErrors.SampleDuplicate);
        }

        Apply(sample, request, hash);
        sample.UpdatedAt = Now();
        // Flag (kể cả đã xử lý) mô tả nội dung cũ; nội dung mới phải được chạy rule lại từ đầu.
        _data.RemoveFlags(await _data.GetFlagsAsync([sample.Id], unresolvedOnly: false, cancellationToken));
        await _data.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(sample));
    }

    /// <summary>Gán nội dung; mọi thay đổi nội dung đưa sample về Raw/NotReviewed (phải chạy rule và duyệt lại).</summary>
    private static void Apply(AiDatasetSample sample, SaveDatasetSampleDto request, string hash)
    {
        sample.SourceType = request.SourceType;
        sample.SourceRefId = request.SourceRefId;
        sample.Instruction = request.Instruction.Trim();
        sample.InputJson = SampleHasher.CanonicalJson(request.InputJson);
        sample.OutputJson = SampleHasher.CanonicalJson(request.OutputJson);
        sample.Language = request.Language.Trim().ToLowerInvariant();
        sample.SplitType = request.SplitType;
        sample.ContentHash = hash;
        sample.QualityStatus = DatasetRules.Quality.Raw;
        sample.ReviewStatus = DatasetRules.Review.NotReviewed;
        sample.QualityScore = null;
        sample.IsPiiRedacted = false;
        sample.ReviewedBy = null;
        sample.ReviewedAt = null;
        sample.ReviewNote = null;
    }

    private static Error? Validate(SaveDatasetSampleDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Instruction) || request.Instruction.Length > InstructionMaxLength)
        {
            return Error.Validation("SAMPLE_INSTRUCTION_INVALID", "Instruction không được trống và tối đa 100000 ký tự.");
        }

        if (request.InputJson.Length > JsonMaxLength || request.OutputJson.Length > JsonMaxLength
            || !SampleHasher.IsValidJson(request.InputJson) || !SampleHasher.IsValidJson(request.OutputJson))
        {
            return Error.Validation("SAMPLE_JSON_INVALID", "InputJson/OutputJson phải là JSON hợp lệ (tối đa 1 MB).");
        }

        if (!DatasetRules.SourceTypes.Contains(request.SourceType))
        {
            return Error.Validation("SAMPLE_SOURCE_TYPE_INVALID", "SourceType không hợp lệ.");
        }

        if (!DatasetRules.SplitTypes.Contains(request.SplitType))
        {
            return Error.Validation("SAMPLE_SPLIT_INVALID", "SplitType phải là Train, Validation hoặc Test.");
        }

        return request.Language.Trim().Length is < 2 or > 10
            ? Error.Validation("SAMPLE_LANGUAGE_INVALID", "Mã ngôn ngữ từ 2 đến 10 ký tự.")
            : null;
    }

    private DateTime Now() => _clock.GetUtcNow().UtcDateTime;

    internal static DatasetDto ToDto(AiDataset d) => new()
    {
        Id = d.Id,
        Code = d.Code,
        Name = d.Name,
        TaskType = d.TaskType,
        Description = d.Description,
        Languages = string.IsNullOrEmpty(d.Languages) ? [] : JsonSerializer.Deserialize<List<string>>(d.Languages) ?? [],
        CreatedAt = d.CreatedAt
    };

    internal static DatasetVersionDto ToDto(AiDatasetVersion v) => new()
    {
        Id = v.Id,
        DatasetId = v.DatasetId,
        VersionTag = v.VersionTag,
        SampleCount = v.SampleCount,
        TrainCount = v.TrainCount,
        ValidationCount = v.ValidationCount,
        TestCount = v.TestCount,
        SplitSeed = v.SplitSeed,
        Checksum = v.Checksum,
        IsFrozen = v.IsFrozen,
        FrozenAt = v.FrozenAt,
        Notes = v.Notes,
        CreatedAt = v.CreatedAt
    };

    internal static DatasetSampleDto ToDto(AiDatasetSample s) => new()
    {
        Id = s.Id,
        DatasetVersionId = s.DatasetVersionId,
        SourceType = s.SourceType,
        SourceRefId = s.SourceRefId,
        Instruction = s.Instruction,
        InputJson = s.InputJson,
        OutputJson = s.OutputJson,
        Language = s.Language,
        TokenCount = s.TokenCount,
        SplitType = s.SplitType,
        QualityStatus = s.QualityStatus,
        QualityScore = s.QualityScore,
        IsPiiRedacted = s.IsPiiRedacted,
        ContentHash = s.ContentHash,
        ReviewStatus = s.ReviewStatus,
        ReviewedBy = s.ReviewedBy,
        ReviewedAt = s.ReviewedAt,
        ReviewNote = s.ReviewNote,
        CreatedAt = s.CreatedAt
    };
}

public static class DataOpsErrors
{
    public static readonly Error DatasetNotFound = Error.NotFound("DATASET_NOT_FOUND", "Không tìm thấy dataset.");
    public static readonly Error DatasetCodeInvalid = Error.Validation("DATASET_CODE_INVALID", "Mã dataset gồm chữ thường, số, '.', '-' (2–80 ký tự).");
    public static readonly Error DatasetCodeDuplicate = Error.Conflict("DATASET_CODE_DUPLICATE", "Mã dataset đã tồn tại.");
    public static readonly Error VersionNotFound = Error.NotFound("DATASET_VERSION_NOT_FOUND", "Không tìm thấy dataset version.");
    public static readonly Error VersionFrozen = Error.Conflict("DATASET_VERSION_FROZEN", "Version đã đóng băng, không được thay đổi mẫu. Hãy tạo version mới.");
    public static readonly Error SampleNotFound = Error.NotFound("DATASET_SAMPLE_NOT_FOUND", "Không tìm thấy mẫu.");
    public static readonly Error SampleDuplicate = Error.Conflict("DATASET_SAMPLE_DUPLICATE", "Mẫu có nội dung trùng đã tồn tại trong version này.");
}
