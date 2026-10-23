using DeliveryIntelligence.Application.Dto;
using ProjectMgmt.BuildingBlocks.Results;
using ProjectMgmt.ProjectManagement.Contracts;

namespace DeliveryIntelligence.Application.IServices;

/// <summary>
/// SCRUMAI-193: chuyển trạng thái Issue theo workflow của Planning. Cần IWorkflowValidationService thật nên chưa đăng ký
/// trong Host; luồng đổi trạng thái cũ trong WorkItemsController vẫn chạy cho tới khi B bàn giao.
/// </summary>
public interface IIssueTransitionService
{
    /// <summary>Kiểm trước để Host biết transition có cần quyền riêng (RequiredPermissionCode) hay không.</summary>
    Task<Result<WorkflowTransitionCheck>> CheckAsync(Guid issueId, Guid targetStatusId, CancellationToken cancellationToken = default);

    Task<Result<IssueTransitionResultDto>> TransitionAsync(
        Guid issueId,
        TransitionIssueDto request,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}
