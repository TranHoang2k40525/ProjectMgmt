using ProjectMgmt.IdentityAccess.Contracts;
using IdentityExperience.Domain.Entities;
using IdentityExperience.Infrastructure.IRepository;

namespace ProjectMgmt.Modules.IdentityExperience.Application.Services;

public class UserLookupService : IUserLookupService, IUserSkillService
{
    private readonly IIdentityRepository _repository;

    public UserLookupService(IIdentityRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<UserDisplayInfo>> GetAllActiveDisplayInfoAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var users = await _repository.Gets<User>(user => user.IsActive);
        return await GetDisplayInfoAsync(users.Select(user => user.Id), cancellationToken);
    }

    public async Task<IReadOnlyList<UserDisplayInfo>> GetDisplayInfoAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var ids = userIds.Distinct().ToArray();
        var users = await _repository.Gets<User>(user => user.IsActive && ids.Contains(user.Id));
        var profiles = (await _repository.Gets<UserProfile>(profile => ids.Contains(profile.UserId)))
            .ToDictionary(profile => profile.UserId);
        return users.Select(user => profiles.TryGetValue(user.Id, out var profile)
            ? new UserDisplayInfo(user.Id, profile.DisplayName, profile.AvatarUrl)
            : new UserDisplayInfo(user.Id, user.Email.Split('@')[0], null))
            .OrderBy(user => user.DisplayName).ToList();
    }

    public async Task<UserDisplayInfo?> GetDisplayInfoAsync(Guid userId, CancellationToken cancellationToken = default) =>
        (await GetDisplayInfoAsync([userId], cancellationToken)).SingleOrDefault();

    public Task<IReadOnlyList<UserSkillInfo>> GetUserSkillsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
    {
        var list = new List<UserSkillInfo>();
        return Task.FromResult<IReadOnlyList<UserSkillInfo>>(list);
    }
}
