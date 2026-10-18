using IdentityExperience.Domain.IRepositories;
using ProjectMgmt.IdentityAccess.Contracts;

namespace ProjectMgmt.Modules.IdentityExperience.Application.Services;

public class UserLookupService : IUserLookupService, IUserSkillService
{
    private static readonly IReadOnlyList<UserDisplayInfo> FallbackUsers = new List<UserDisplayInfo>
    {
        new UserDisplayInfo
        {
            UserId = Guid.Parse("11111111-0000-0000-0000-000000000001"),
            DisplayName = "Hoàng Admin (System Lead)"
        },
        new UserDisplayInfo
        {
            UserId = Guid.Parse("11111111-0000-0000-0000-000000000002"),
            DisplayName = "Nguyễn Văn Dev (Technical Lead)"
        },
        new UserDisplayInfo
        {
            UserId = Guid.Parse("11111111-0000-0000-0000-000000000003"),
            DisplayName = "Trần Thị Scrum (Scrum Master)"
        }
    };

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
        var users = await GetDisplayInfoAsync(FallbackUsers.Select(u => u.UserId), cancellationToken);
        return users.Count > 0 ? users : FallbackUsers;
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
            else
            {
                var fallback = FallbackUsers.FirstOrDefault(f => f.UserId == userId);
                if (fallback is not null)
                {
                    results.Add(fallback);
                }
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
                DisplayName = details.Profile.DisplayName,
                AvatarUrl = details.Profile.AvatarUrl
            };
        }

        return FallbackUsers.FirstOrDefault(f => f.UserId == userId);
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
