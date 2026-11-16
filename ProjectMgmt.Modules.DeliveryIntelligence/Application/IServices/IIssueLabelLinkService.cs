using DeliveryIntelligence.Application.Dto;
using ProjectMgmt.BuildingBlocks.Results;

namespace DeliveryIntelligence.Application.IServices;

/// <summary>SCRUMAI-185: nhãn theo Project và liên kết Blocks/Relates/Duplicates giữa các Issue cùng Project.</summary>
public interface IIssueLabelLinkService
{
    Task<IReadOnlyList<LabelDto>> GetProjectLabelsAsync(Guid projectId);

    Task<Result<LabelDto>> CreateLabelAsync(Guid projectId, SaveLabelDto request);

    Task<Result<LabelDto>> UpdateLabelAsync(Guid labelId, SaveLabelDto request);

    Task<Result> DeleteLabelAsync(Guid labelId);

    Task<Result<Guid>> GetLabelProjectIdAsync(Guid labelId);

    Task<Result<IReadOnlyList<LabelDto>>> GetIssueLabelsAsync(Guid issueId);

    Task<Result<IReadOnlyList<LabelDto>>> AddLabelToIssueAsync(Guid issueId, Guid labelId, Guid actorUserId);

    Task<Result<IReadOnlyList<LabelDto>>> RemoveLabelFromIssueAsync(Guid issueId, Guid labelId, Guid actorUserId);

    Task<Result<IReadOnlyList<IssueLinkDto>>> GetLinksAsync(Guid issueId);

    Task<Result<IssueLinkDto>> CreateLinkAsync(Guid issueId, CreateIssueLinkDto request, Guid actorUserId);

    /// <summary>Xóa liên kết cùng bản ghi ngược chiều.</summary>
    Task<Result> DeleteLinkAsync(Guid linkId, Guid actorUserId);

    Task<Result<Guid>> GetLinkSourceIssueIdAsync(Guid linkId);
}
