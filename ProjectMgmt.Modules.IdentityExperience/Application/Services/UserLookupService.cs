using ProjectMgmt.IdentityAccess.Contracts;

namespace ProjectMgmt.Modules.IdentityExperience.Application.Services;

public class UserLookupService : IUserLookupService, IUserSkillService
{
    public Task<IReadOnlyList<UserDisplayInfo>> GetDisplayInfoAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
    {
        var list = userIds.Select(id => new UserDisplayInfo(id, $"User {id.ToString()[..8]}", null)).ToList();
        return Task.FromResult<IReadOnlyList<UserDisplayInfo>>(list);
    }

    public Task<UserDisplayInfo?> GetDisplayInfoAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<UserDisplayInfo?>(new UserDisplayInfo(userId, $"User {userId.ToString()[..8]}", null));
    }

    public Task<IReadOnlyList<UserSkillInfo>> GetUserSkillsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
    {
        var list = new List<UserSkillInfo>();
        return Task.FromResult<IReadOnlyList<UserSkillInfo>>(list);
    }
}
