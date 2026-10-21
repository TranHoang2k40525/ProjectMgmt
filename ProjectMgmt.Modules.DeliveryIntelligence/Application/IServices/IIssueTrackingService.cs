using DeliveryIntelligence.Application.Dto;
using ProjectMgmt.BuildingBlocks.Results;

namespace DeliveryIntelligence.Application.IServices;

/// <summary>
/// Use case Issue nội bộ module DeliveryIntelligence. Contract công bố cho A/B (IIssueService, IIssueReadService…)
/// được dựng trên service này ở các bước sau; quyền theo Project do tầng gọi kiểm trước.
/// </summary>
public interface IIssueTrackingService
{
    Task<Result<IssueDetailDto>> GetAsync(Guid issueId, CancellationToken cancellationToken = default);

    Task<Result<PagedResultDto<IssueListItemDto>>> ListAsync(
        Guid projectId,
        IssueListQueryDto query,
        CancellationToken cancellationToken = default);

    Result ValidateCreate(CreateIssueDto request);

    Result ValidateUpdate(UpdateIssueDto request);
}
