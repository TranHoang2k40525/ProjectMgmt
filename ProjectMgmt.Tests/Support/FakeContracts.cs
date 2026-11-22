using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.ProjectManagement.Contracts;

namespace ProjectMgmt.Tests.Support;

/// <summary>
/// Fake contract liên module dựng trên <see cref="SharedTestData"/>: consumer phát triển song song mà không cần
/// module provider thật. Fake chỉ dùng trong test, không đăng ký vào Host.
/// </summary>
public class SharedProjectLookupService : IProjectLookupService
{
    public Task<IReadOnlyList<ProjectIssueTypeDto>> GetIssueTypesAsync(Guid projectId)
    {
        IReadOnlyList<ProjectIssueTypeDto> result = SharedTestData.IssueTypes.Where(type => type.ProjectId == projectId).ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<ProjectStatusDto>> GetStatusesAsync(Guid projectId)
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
    public Task<IReadOnlyList<UserDisplayInfo>> GetAllActiveDisplayInfoAsync() =>
        Task.FromResult(SharedTestData.Users);

    public Task<IReadOnlyList<UserDisplayInfo>> GetDisplayInfoAsync(IEnumerable<Guid> userIds)
    {
        var ids = userIds.ToHashSet();
        IReadOnlyList<UserDisplayInfo> result = SharedTestData.Users.Where(user => ids.Contains(user.UserId)).ToList();
        return Task.FromResult(result);
    }

    public Task<UserDisplayInfo?> GetDisplayInfoAsync(Guid userId) =>
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

    public Task<int> ReserveNextAsync(Guid projectId)
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

    public Task<WorkflowTransitionCheck> CheckTransitionAsync(Guid projectId, Guid fromStatusId, Guid toStatusId)
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

/// <summary>Component/Version mẫu: DEMO có 2 component + 2 version; OTHER có 1 component (để kiểm chặn khác Project).</summary>
public class SharedReleaseLookupService : IProjectReleaseLookupService
{
    public static readonly Guid BackendComponentId = Guid.Parse("c0de0000-0000-0000-0008-000000000001");
    public static readonly Guid FrontendComponentId = Guid.Parse("c0de0000-0000-0000-0008-000000000002");
    public static readonly Guid OtherComponentId = Guid.Parse("c0de0000-0000-0000-0008-000000000003");
    public static readonly Guid V1VersionId = Guid.Parse("c0de0000-0000-0000-0009-000000000001");
    public static readonly Guid V2VersionId = Guid.Parse("c0de0000-0000-0000-0009-000000000002");

    public Task<IReadOnlyList<ProjectComponentDto>> GetComponentsAsync(Guid projectId)
    {
        IReadOnlyList<ProjectComponentDto> all =
        [
            new(BackendComponentId, SharedTestData.ProjectId, "Backend API", SharedTestData.DotnetSkillId),
            new(FrontendComponentId, SharedTestData.ProjectId, "Web UI", SharedTestData.AngularSkillId),
            new(OtherComponentId, SharedTestData.OtherProjectId, "Khác", null)
        ];
        return Task.FromResult<IReadOnlyList<ProjectComponentDto>>(all.Where(c => c.ProjectId == projectId).ToList());
    }

    public Task<IReadOnlyList<ProjectVersionDto>> GetVersionsAsync(Guid projectId)
    {
        IReadOnlyList<ProjectVersionDto> all =
        [
            new(V1VersionId, SharedTestData.ProjectId, "v1.0", new DateOnly(2026, 11, 29), false),
            new(V2VersionId, SharedTestData.ProjectId, "v0.9", new DateOnly(2026, 11, 22), true)
        ];
        return Task.FromResult<IReadOnlyList<ProjectVersionDto>>(all.Where(v => v.ProjectId == projectId).ToList());
    }
}

public class RecordingIssueNotifier : DeliveryIntelligence.Application.IServices.IIssueNotifier
{
    public List<DeliveryIntelligence.Application.IServices.IssueNotification> Sent { get; } = [];

    public Task NotifyAsync(DeliveryIntelligence.Application.IServices.IssueNotification notification)
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
