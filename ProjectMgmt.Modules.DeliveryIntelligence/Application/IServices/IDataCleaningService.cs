using DeliveryIntelligence.Application.Dto;
using ProjectMgmt.BuildingBlocks.Results;

namespace DeliveryIntelligence.Application.IServices;

/// <summary>
/// SCRUMAI-212 (phần BE): rule làm sạch dạng cấu hình, chạy rule trên sample để sinh quality flag và cập nhật QualityStatus.
/// Không gồm phần AI (chấm điểm chất lượng bằng mô hình).
/// </summary>
public interface IDataCleaningService
{
    Task<IReadOnlyList<CleaningRuleDto>> ListRulesAsync(CancellationToken cancellationToken = default);

    Task<Result<CleaningRuleDto>> CreateRuleAsync(CreateCleaningRuleDto request, CancellationToken cancellationToken = default);

    Task<Result<CleaningRuleDto>> UpdateRuleAsync(Guid ruleId, UpdateCleaningRuleDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Chạy lại mọi rule đang bật trên các sample của version: flag chưa xử lý của lần chạy trước được thay bằng kết quả mới
    /// (flag đã xử lý được giữ làm lịch sử). Có flag Error ⇒ Rejected; còn lại Raw ⇒ Cleaned.
    /// </summary>
    Task<Result<CleaningRunSummaryDto>> RunAsync(Guid versionId, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<QualityFlagDto>>> GetSampleFlagsAsync(Guid sampleId, CancellationToken cancellationToken = default);

    Task<Result<QualityFlagDto>> ResolveFlagAsync(Guid flagId, Guid actorUserId, CancellationToken cancellationToken = default);
}
