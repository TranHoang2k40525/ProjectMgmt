using DeliveryIntelligence.Application.Dto;
using ProjectMgmt.BuildingBlocks.Results;

namespace DeliveryIntelligence.Application.IServices;

/// <summary>
/// Use case Issue nội bộ module DeliveryIntelligence. Contract công bố cho A/B (IIssueService, IIssueReadService…)
/// được dựng trên service này; quyền theo Project do tầng gọi (Host) kiểm trước.
/// </summary>
public interface IIssueTrackingService
{
    Task<Result<IssueDetailDto>> GetAsync(Guid issueId);

    Task<Result<PagedResultDto<IssueListItemDto>>> ListAsync(
        Guid projectId,
        IssueListQueryDto query);

    Task<Result<IssueDetailDto>> UpdateAsync(
        Guid issueId,
        UpdateIssueDto request,
        Guid actorUserId);

    /// <summary>Xóa mềm Issue cùng toàn bộ con cháu; Issue khác đang trỏ EpicId tới nó được gỡ liên kết.</summary>
    Task<Result> DeleteAsync(Guid issueId, Guid actorUserId);

    Result ValidateCreate(CreateIssueDto request);

    Result ValidateUpdate(UpdateIssueDto request);
}

/// <summary>
/// Tạo Issue thật: cần IIssueNumberGenerator của Planning. Chưa đăng ký trong Host cho tới khi B có implementation thật;
/// trong lúc đó WorkItemsController vẫn dùng luồng tạo Issue cũ.
/// </summary>
public interface IIssueCreationService
{
    Task<Result<IssueDetailDto>> CreateAsync(
        CreateIssueDto request,
        Guid reporterUserId);
}
