using DeliveryIntelligence.Application.Dto;
using ProjectMgmt.BuildingBlocks.Results;

namespace DeliveryIntelligence.Application.IServices;

/// <summary>
/// SCRUMAI-203: gắn Issue với nhiều Component và Fix/Affects Version cùng Project.
/// Cần IProjectReleaseLookupService của Planning (chưa có bản thật) nên Host chỉ dựng service khi contract đó tồn tại.
/// </summary>
public interface IIssueReleaseService
{
    Task<Result<IssueReleaseLinksDto>> GetLinksAsync(Guid issueId);

    Task<Result<IssueReleaseLinksDto>> AddComponentAsync(Guid issueId, Guid componentId, Guid actorUserId);

    Task<Result<IssueReleaseLinksDto>> RemoveComponentAsync(Guid issueId, Guid componentId, Guid actorUserId);

    Task<Result<IssueReleaseLinksDto>> AddVersionAsync(Guid issueId, AddIssueVersionDto request, Guid actorUserId);

    Task<Result<IssueReleaseLinksDto>> RemoveVersionAsync(Guid issueId, Guid versionId, string linkType, Guid actorUserId);
}
