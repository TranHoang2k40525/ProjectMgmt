namespace ProjectMgmt.ProjectManagement.Contracts;

/// <summary>Kết quả kiểm một đường chuyển trạng thái tại thời điểm command.</summary>
public record WorkflowTransitionCheck(
    bool Allowed,
    string? ReasonCode,
    string? Reason,
    string? RequiredPermissionCode,
    string? FromCategory,
    string ToCategory);

/// <summary>
/// Planning (B) sở hữu WorkflowStatus/WorkflowTransition và kiểm transition; DeliveryIntelligence (C) là consumer.
/// Chưa có implementation thật trong Host: C chỉ dùng fake trong test cho tới khi B bàn giao.
/// </summary>
public interface IWorkflowValidationService
{
    Task<WorkflowTransitionCheck> CheckTransitionAsync(
        Guid projectId,
        Guid fromStatusId,
        Guid toStatusId,
        CancellationToken cancellationToken = default);
}
