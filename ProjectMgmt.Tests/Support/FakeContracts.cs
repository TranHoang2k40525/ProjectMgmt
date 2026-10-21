using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.ProjectManagement.Contracts;

namespace ProjectMgmt.Tests.Support;

/// <summary>
/// Fake contract liên module dựng trên <see cref="SharedTestData"/>: consumer phát triển song song mà không cần
/// module provider thật. Fake chỉ dùng trong test, không đăng ký vào Host.
/// </summary>
public class SharedProjectLookupService : IProjectLookupService
{
    public Task<IReadOnlyList<ProjectIssueTypeDto>> GetIssueTypesAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ProjectIssueTypeDto> result = SharedTestData.IssueTypes.Where(type => type.ProjectId == projectId).ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<ProjectStatusDto>> GetStatusesAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ProjectStatusDto> result = SharedTestData.Statuses.Where(status => status.ProjectId == projectId).ToList();
        return Task.FromResult(result);
    }

    public Task<ProjectScopeInfo?> GetProjectScopeAsync(Guid projectId)
    {
        var known = projectId == SharedTestData.ProjectId || projectId == SharedTestData.OtherProjectId;
        return Task.FromResult(known
            ? new ProjectScopeInfo { ProjectId = projectId, OrganizationId = SharedTestData.OrganizationId }
            : null);
    }
}

public class SharedUserLookupService : IUserLookupService
{
    public Task<IReadOnlyList<UserDisplayInfo>> GetAllActiveDisplayInfoAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SharedTestData.Users);

    public Task<IReadOnlyList<UserDisplayInfo>> GetDisplayInfoAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds.ToHashSet();
        IReadOnlyList<UserDisplayInfo> result = SharedTestData.Users.Where(user => ids.Contains(user.UserId)).ToList();
        return Task.FromResult(result);
    }

    public Task<UserDisplayInfo?> GetDisplayInfoAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(SharedTestData.Users.SingleOrDefault(user => user.UserId == userId));
}

/// <summary>Fake cấp số: tiếp nối IssueCounter của seed, an toàn khi gọi song song trong một test.</summary>
public class SharedIssueNumberGenerator : IIssueNumberGenerator
{
    private readonly Dictionary<Guid, int> _counters = new()
    {
        [SharedTestData.ProjectId] = SharedTestData.IssueCounter(SharedTestData.ProjectId),
        [SharedTestData.OtherProjectId] = SharedTestData.IssueCounter(SharedTestData.OtherProjectId)
    };

    public Task<int> ReserveNextAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        lock (_counters)
        {
            _counters[projectId] = _counters.GetValueOrDefault(projectId) + 1;
            return Task.FromResult(_counters[projectId]);
        }
    }
}

/// <summary>Fake kiểm workflow theo <see cref="SharedTestData.Transitions"/>; có thể đặt quyền riêng cho một đường chuyển.</summary>
public class SharedWorkflowValidationService : IWorkflowValidationService
{
    public Dictionary<(Guid From, Guid To), string> RequiredPermissions { get; } = [];

    public Task<WorkflowTransitionCheck> CheckTransitionAsync(Guid projectId, Guid fromStatusId, Guid toStatusId, CancellationToken cancellationToken = default)
    {
        var from = SharedTestData.Statuses.SingleOrDefault(s => s.Id == fromStatusId && s.ProjectId == projectId);
        var to = SharedTestData.Statuses.SingleOrDefault(s => s.Id == toStatusId && s.ProjectId == projectId);
        var allowed = from is not null && to is not null && SharedTestData.Transitions.Any(t => t.From == fromStatusId && t.To == toStatusId);
        RequiredPermissions.TryGetValue((fromStatusId, toStatusId), out var permission);
        return Task.FromResult(new WorkflowTransitionCheck(
            allowed,
            allowed ? null : "WORKFLOW_TRANSITION_NOT_ALLOWED",
            allowed ? null : $"Không có đường chuyển {from?.Name} → {to?.Name}.",
            permission,
            from?.Category,
            to?.Category ?? string.Empty));
    }
}

public class RecordingIssueNotifier : DeliveryIntelligence.Application.IServices.IIssueNotifier
{
    public List<DeliveryIntelligence.Application.IServices.IssueNotification> Sent { get; } = [];

    public Task NotifyAsync(DeliveryIntelligence.Application.IServices.IssueNotification notification, CancellationToken cancellationToken = default)
    {
        Sent.Add(notification);
        return Task.CompletedTask;
    }
}

/// <summary>Quyền mặc định: thành viên DEMO đọc được; chỉ những mã quyền được Grant mới trả true.</summary>
public class SharedPermissionEvaluator : IPermissionEvaluator
{
    private readonly HashSet<(Guid UserId, string Code)> _grants = [];

    public SharedPermissionEvaluator Grant(Guid userId, params string[] permissionCodes)
    {
        foreach (var code in permissionCodes)
        {
            _grants.Add((userId, code));
        }

        return this;
    }

    public Task<bool> HasPermissionAsync(
        Guid userId,
        string permissionCode,
        string scopeType,
        Guid? scopeId,
        Guid? organizationId = null) =>
        Task.FromResult(_grants.Contains((userId, permissionCode)));

    public Task<bool> IsProjectMemberAsync(Guid userId, Guid projectId) =>
        Task.FromResult(projectId == SharedTestData.ProjectId
            && SharedTestData.DemoMembers.Any(member => member.UserId == userId));
}
