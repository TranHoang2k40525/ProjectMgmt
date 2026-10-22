using DeliveryIntelligence.Application.Dto;
using ProjectMgmt.BuildingBlocks.Results;

namespace DeliveryIntelligence.Application.IServices;

/// <summary>SCRUMAI-213: duyệt mẫu và đóng băng version (nền cho màn DataOps).</summary>
public interface IDatasetReviewService
{
    /// <summary>
    /// Chỉ duyệt mẫu đã qua rule (Cleaned), không còn flag Error hay PII chưa xử lý, và người duyệt xác nhận đã kiểm PII.
    /// </summary>
    Task<Result<DatasetSampleDto>> ApproveAsync(Guid sampleId, ApproveSampleDto request, Guid reviewerUserId, CancellationToken cancellationToken = default);

    Task<Result<DatasetSampleDto>> RejectAsync(Guid sampleId, RejectSampleDto request, Guid reviewerUserId, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<QualityFlagDto>>> GetVersionFlagsAsync(Guid versionId, bool unresolvedOnly, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đóng băng version khi mọi mẫu đã có quyết định (Approved/Rejected), có ít nhất một mẫu Approved và mẫu Approved
    /// đều đã kiểm PII, không còn flag Error/PII mở. Chốt SplitSeed, số mẫu theo split và checksum manifest.
    /// </summary>
    Task<Result<DatasetVersionDto>> FreezeAsync(Guid versionId, FreezeVersionDto request, CancellationToken cancellationToken = default);
}
