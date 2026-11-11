using DeliveryIntelligence.Application.Dto;
using ProjectMgmt.BuildingBlocks.Results;

namespace DeliveryIntelligence.Application.IServices;

/// <summary>SCRUMAI-176: đổi cha theo luật Epic → Story/Task → Sub-task và sắp xếp RankOrder trong Backlog.</summary>
public interface IIssueHierarchyService
{
    Task<Result<IssueDetailDto>> ChangeParentAsync(
        Guid issueId,
        ChangeIssueParentDto request,
        Guid actorUserId);

    Task<Result<IssueRankDto>> RankAsync(
        Guid issueId,
        RankIssueDto request,
        Guid actorUserId);

    /// <summary>Đánh lại RankOrder 1..n theo thứ tự hiện tại khi các rank quá sát nhau.</summary>
    Task<Result<int>> RebalanceAsync(Guid projectId);

    Task<Result<IReadOnlyList<IssueListItemDto>>> GetSubtasksAsync(
        Guid parentIssueId);

    Task<Result<IReadOnlyList<IssueListItemDto>>> GetEpicChildrenAsync(
        Guid epicId);
}
