using IdentityExperience.Domain.IRepositories;
using ProjectMgmt.IdentityAccess.Contracts;

namespace ProjectMgmt.Modules.IdentityExperience.Application.Services;

public class UserLookupService : IUserLookupService, IUserSkillService
{
    private readonly IProfileRepository _profileRepository;
    private readonly ISkillRepository _skillRepository;

    public UserLookupService(
        IProfileRepository profileRepository,
        ISkillRepository skillRepository)
    {
        _profileRepository = profileRepository;
        _skillRepository = skillRepository;
    }

    public async Task<IReadOnlyList<UserDisplayInfo>> GetAllActiveDisplayInfoAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var profiles = await _profileRepository.GetAllActiveProfilesAsync(cancellationToken);
        if (profiles.Count > 0)
        {
            return profiles.Select(p => new UserDisplayInfo
            {
                UserId = p.UserId,
                DisplayName = !string.IsNullOrWhiteSpace(p.DisplayName) ? p.DisplayName : p.Email,
                AvatarUrl = p.AvatarUrl
            }).ToList();
        }

        return Array.Empty<UserDisplayInfo>();
    }

    public async Task<IReadOnlyList<UserDisplayInfo>> GetDisplayInfoAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var results = new List<UserDisplayInfo>();
        foreach (var userId in userIds.Distinct())
        {
            var user = await GetDisplayInfoAsync(userId, cancellationToken);
            if (user is not null)
            {
                results.Add(user);
            }
        }

        return results;
    }

    public async Task<UserDisplayInfo?> GetDisplayInfoAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var details = await _profileRepository.GetProfileDetailsAsync(userId);
        if (details is not null)
        {
            return new UserDisplayInfo
            {
                UserId = userId,
                DisplayName = !string.IsNullOrWhiteSpace(details.Profile.DisplayName)
                    ? details.Profile.DisplayName
                    : details.User.Email,
                AvatarUrl = details.Profile.AvatarUrl
            };
        }

        return null;
    }

    public async Task<IReadOnlyList<UserSkillInfo>> GetUserSkillsAsync(IEnumerable<Guid> userIds)
    {
        var results = new List<UserSkillInfo>();
        foreach (var userId in userIds.Distinct())
        {
            var skills = await _skillRepository.GetUserSkillsAsync(userId);
            results.AddRange(skills.Select(skill => new UserSkillInfo
            {
                UserId = userId,
                SkillName = skill.Skill.Name,
                ProficiencyLevel = skill.UserSkill.ProficiencyLevel
            }));
        }

        return results;
    }
}
