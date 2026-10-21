using DeliveryIntelligence.Application.Dto;
using ProjectMgmt.BuildingBlocks.Results;

namespace DeliveryIntelligence.Application.IServices;

/// <summary>SCRUMAI-202: Acceptance Criteria (thêm/sửa/tick/xóa/sắp xếp) và kỹ năng yêu cầu của Issue.</summary>
public interface IIssueRequirementService
{
    Task<Result<IReadOnlyList<AcceptanceCriterionDto>>> GetCriteriaAsync(Guid issueId, CancellationToken cancellationToken = default);

    Task<Result<AcceptanceCriterionDto>> AddCriterionAsync(Guid issueId, SaveCriterionDto request, Guid actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Sửa nội dung/tick; tick ghi người và thời điểm đạt, sửa nội dung tiêu chí do AI sinh thì đánh dấu WasEditedAfterAi.</summary>
    Task<Result<AcceptanceCriterionDto>> UpdateCriterionAsync(Guid criterionId, SaveCriterionDto request, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<Result> DeleteCriterionAsync(Guid criterionId, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<AcceptanceCriterionDto>>> ReorderCriteriaAsync(Guid issueId, ReorderCriteriaDto request, CancellationToken cancellationToken = default);

    Task<Result<Guid>> GetCriterionIssueIdAsync(Guid criterionId, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<RequiredSkillDto>>> GetRequiredSkillsAsync(Guid issueId, CancellationToken cancellationToken = default);

    Task<Result<RequiredSkillDto>> AddRequiredSkillAsync(Guid issueId, SaveRequiredSkillDto request, Guid actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Đổi mức/trọng số; Source giữ nguyên (kỹ năng do AI đề xuất vẫn là AI).</summary>
    Task<Result<RequiredSkillDto>> UpdateRequiredSkillAsync(Guid requiredSkillId, SaveRequiredSkillDto request, CancellationToken cancellationToken = default);

    Task<Result> DeleteRequiredSkillAsync(Guid requiredSkillId, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<Result<Guid>> GetRequiredSkillIssueIdAsync(Guid requiredSkillId, CancellationToken cancellationToken = default);
}

public record SkillCatalogItem(Guid Id, string Name, string? Category, bool IsActive);

/// <summary>Port đọc danh mục kỹ năng (IdentityExperience sở hữu). Adapter ở Host.</summary>
public interface ISkillCatalogReader
{
    Task<IReadOnlyList<SkillCatalogItem>> GetSkillsAsync(CancellationToken cancellationToken = default);
}
