namespace ProjectMgmt.ProjectManagement.Contracts;

public record ProjectComponentDto(Guid Id, Guid ProjectId, string Name, Guid? DefaultSkillId);

public record ProjectVersionDto(Guid Id, Guid ProjectId, string Name, DateOnly? ReleaseDate, bool IsReleased);

/// <summary>
/// Planning (B) sở hữu ProjectComponent/ProjectVersion. DeliveryIntelligence (C) dùng để kiểm và hiển thị liên kết Issue.
/// Chưa có implementation thật trong Host: C chỉ dùng fake trong test cho tới khi B bàn giao.
/// </summary>
public interface IProjectReleaseLookupService
{
    Task<IReadOnlyList<ProjectComponentDto>> GetComponentsAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectVersionDto>> GetVersionsAsync(Guid projectId, CancellationToken cancellationToken = default);
}
