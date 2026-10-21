using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.ProjectManagement.Contracts;

namespace ProjectMgmt.Tests.Support;

/// <summary>
/// Fake contract liên module dựng trên <see cref="SharedTestData"/>: consumer phát triển song song mà không cần
/// module provider thật. Fake chỉ dùng trong test, không đăng ký vào Host.
/// </summary>
public class FakeProjectLookupService : IProjectLookupService
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

public class FakeUserLookupService : IUserLookupService
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

/// <summary>Quyền mặc định: thành viên DEMO đọc được; chỉ những mã quyền được Grant mới trả true.</summary>
public class FakePermissionEvaluator : IPermissionEvaluator
{
    private readonly HashSet<(Guid UserId, string Code)> _grants = [];

    public FakePermissionEvaluator Grant(Guid userId, params string[] permissionCodes)
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
