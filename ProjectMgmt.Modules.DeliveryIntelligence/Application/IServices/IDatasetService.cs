using DeliveryIntelligence.Application.Dto;
using ProjectMgmt.BuildingBlocks.Results;

namespace DeliveryIntelligence.Application.IServices;

/// <summary>
/// SCRUMAI-211: vòng đời Dataset → Version → Sample. Version đã freeze là bất biến: mọi thao tác ghi sample bị từ chối.
/// Quyền "ai.dataset.manage" (phạm vi hệ thống) do Host kiểm.
/// </summary>
public interface IDatasetService
{
    Task<IReadOnlyList<DatasetDto>> ListDatasetsAsync();

    Task<Result<DatasetDto>> CreateDatasetAsync(CreateDatasetDto request, Guid actorUserId);

    Task<Result<IReadOnlyList<DatasetVersionDto>>> ListVersionsAsync(Guid datasetId);

    Task<Result<DatasetVersionDto>> CreateVersionAsync(Guid datasetId, CreateDatasetVersionDto request);

    Task<Result<DatasetVersionDto>> GetVersionAsync(Guid versionId);

    Task<Result<PagedResultDto<DatasetSampleDto>>> ListSamplesAsync(Guid versionId, SampleQueryDto query);

    Task<Result<DatasetSampleDto>> GetSampleAsync(Guid sampleId);

    /// <summary>Thêm sample (Raw, chưa duyệt). Nội dung trùng trong cùng version (ContentHash) bị từ chối 409.</summary>
    Task<Result<DatasetSampleDto>> AddSampleAsync(Guid versionId, SaveDatasetSampleDto request);

    /// <summary>Sửa nội dung sample: tính lại hash, đưa về Raw/NotReviewed để chạy lại rule và duyệt lại.</summary>
    Task<Result<DatasetSampleDto>> UpdateSampleAsync(Guid sampleId, SaveDatasetSampleDto request);
}
