using DeliveryIntelligence.Application.Dto;
using ProjectMgmt.BuildingBlocks.Results;

namespace DeliveryIntelligence.Application.IServices;

/// <summary>SCRUMAI-195: đọc ActivityLog (phân trang) và lịch sử trạng thái/người xử lý đã kèm tên hiển thị.</summary>
public interface IIssueHistoryService
{
    Task<Result<PagedResultDto<ActivityDto>>> GetActivityAsync(Guid issueId, int page, int pageSize);

    Task<Result<IReadOnlyList<StatusChangeDto>>> GetStatusHistoryAsync(Guid issueId);

    Task<Result<IReadOnlyList<AssignmentChangeDto>>> GetAssignmentHistoryAsync(Guid issueId);
}
