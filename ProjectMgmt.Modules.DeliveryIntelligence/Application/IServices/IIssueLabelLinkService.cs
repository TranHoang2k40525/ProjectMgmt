using DeliveryIntelligence.Application.Dto;
using ProjectMgmt.BuildingBlocks.Results;

namespace DeliveryIntelligence.Application.IServices;

/// <summary>SCRUMAI-185: nhãn theo Project và liên kết Blocks/Relates/Duplicates giữa các Issue cùng Project.</summary>
public interface IIssueLabelLinkService
{
    Task<IReadOnlyList<LabelDto>> GetProjectLabelsAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<Result<LabelDto>> CreateLabelAsync(Guid projectId, SaveLabelDto request, CancellationToken cancellationToken = default);

    Task<Result<LabelDto>> UpdateLabelAsync(Guid labelId, SaveLabelDto request, CancellationToken cancellationToken = default);

    Task<Result> DeleteLabelAsync(Guid labelId, CancellationToken cancellationToken = default);

    Task<Result<Guid>> GetLabelProjectIdAsync(Guid labelId, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<LabelDto>>> GetIssueLabelsAsync(Guid issueId, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<LabelDto>>> AddLabelToIssueAsync(Guid issueId, Guid labelId, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<LabelDto>>> RemoveLabelFromIssueAsync(Guid issueId, Guid labelId, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<IssueLinkDto>>> GetLinksAsync(Guid issueId, CancellationToken cancellationToken = default);

    Task<Result<IssueLinkDto>> CreateLinkAsync(Guid issueId, CreateIssueLinkDto request, Guid actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Xóa liên kết cùng bản ghi ngược chiều.</summary>
    Task<Result> DeleteLinkAsync(Guid linkId, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<Result<Guid>> GetLinkSourceIssueIdAsync(Guid linkId, CancellationToken cancellationToken = default);
}
