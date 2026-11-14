using DeliveryIntelligence.Application.Dto;
using ProjectMgmt.BuildingBlocks.Results;

namespace DeliveryIntelligence.Application.IServices;

/// <summary>SCRUMAI-202: Acceptance Criteria (thêm/sửa/tick/xóa/sắp xếp) và kỹ năng yêu cầu của Issue.</summary>
public interface IIssueRequirementService
{
    Task<Result<IReadOnlyList<AcceptanceCriterionDto>>> GetCriteriaAsync(Guid issueId);

    Task<Result<AcceptanceCriterionDto>> AddCriterionAsync(Guid issueId, SaveCriterionDto request, Guid actorUserId);

    /// <summary>Sửa nội dung/tick; tick ghi người và thời điểm đạt, sửa nội dung tiêu chí do AI sinh thì đánh dấu WasEditedAfterAi.</summary>
    Task<Result<AcceptanceCriterionDto>> UpdateCriterionAsync(Guid criterionId, SaveCriterionDto request, Guid actorUserId);

    Task<Result> DeleteCriterionAsync(Guid criterionId, Guid actorUserId);

    Task<Result<IReadOnlyList<AcceptanceCriterionDto>>> ReorderCriteriaAsync(Guid issueId, ReorderCriteriaDto request);

    Task<Result<Guid>> GetCriterionIssueIdAsync(Guid criterionId);

    Task<Result<IReadOnlyList<RequiredSkillDto>>> GetRequiredSkillsAsync(Guid issueId);

    Task<Result<RequiredSkillDto>> AddRequiredSkillAsync(Guid issueId, SaveRequiredSkillDto request, Guid actorUserId);

    /// <summary>Đổi mức/trọng số; Source giữ nguyên (kỹ năng do AI đề xuất vẫn là AI).</summary>
    Task<Result<RequiredSkillDto>> UpdateRequiredSkillAsync(Guid requiredSkillId, SaveRequiredSkillDto request);

    Task<Result> DeleteRequiredSkillAsync(Guid requiredSkillId, Guid actorUserId);

    Task<Result<Guid>> GetRequiredSkillIssueIdAsync(Guid requiredSkillId);
}

public record SkillCatalogItem(Guid Id, string Name, string? Category, bool IsActive);

/// <summary>Port đọc danh mục kỹ năng (IdentityExperience sở hữu). Adapter ở Host.</summary>
public interface ISkillCatalogReader
{
    Task<IReadOnlyList<SkillCatalogItem>> GetSkillsAsync();
}
