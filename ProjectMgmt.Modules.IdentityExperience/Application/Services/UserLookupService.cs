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

    public async Task<IReadOnlyList<UserDisplayInfo>> GetDisplayInfoAsync(IEnumerable<Guid> userIds)
    {
        var results = new List<UserDisplayInfo>();
        foreach (var userId in userIds.Distinct())
        {
            var user = await GetDisplayInfoAsync(userId);
            if (user is not null)
            {
                results.Add(user);
            }
        }

        return results;
    }

    public async Task<UserDisplayInfo?> GetDisplayInfoAsync(Guid userId)
    {
        var details = await _profileRepository.GetProfileDetailsAsync(userId);
        return details is null
            ? null
            : new UserDisplayInfo
            {
                UserId = userId,
                DisplayName = details.Profile.DisplayName,
                AvatarUrl = details.Profile.AvatarUrl
            };
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
