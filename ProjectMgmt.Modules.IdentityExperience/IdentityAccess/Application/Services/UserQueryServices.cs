using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.Modules.IdentityExperience.IdentityAccess.Domain.IRepositories;

namespace ProjectMgmt.Modules.IdentityExperience.IdentityAccess.Application.Services;

internal class UserLookupService : IUserLookupService
{
    private readonly IUserRepository _repository;

    public UserLookupService(IUserRepository repository)
    {
        _repository = repository;
    }

    public Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _repository.ExistsActiveAsync(userId, cancellationToken);

    public async Task<UserDisplayInfo?> GetDisplayInfoAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var profile = await _repository.GetActiveProfileAsync(userId, cancellationToken);
        return profile is null ? null : new UserDisplayInfo(profile.UserId, profile.DisplayName, profile.AvatarUrl);
    }

    public async Task<IReadOnlyList<UserDisplayInfo>> GetDisplayInfoAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        return (await _repository.GetActiveProfilesAsync(userIds, cancellationToken))
            .Select(profile => new UserDisplayInfo(profile.UserId, profile.DisplayName, profile.AvatarUrl))
            .ToList();
    }
}

internal class UserSkillService : IUserSkillService
{
    private readonly IUserRepository _repository;

    public UserSkillService(IUserRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<UserSkillInfo>> GetUserSkillsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        return (await _repository.GetSkillsAsync(userIds, cancellationToken))
            .Select(record => new UserSkillInfo(
                record.UserSkill.UserId,
                record.Skill.Code,
                record.Skill.Name,
                record.UserSkill.ProficiencyLevel,
                record.UserSkill.YearsOfExperience,
                record.UserSkill.IsSelfDeclared,
                !record.UserSkill.IsSelfDeclared))
            .ToList();
    }

    public async Task<IReadOnlyList<UserProfileFeatures>> GetProfileFeaturesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        return (await _repository.GetActiveProfilesAsync(userIds, cancellationToken))
            .Select(profile => new UserProfileFeatures(
                profile.UserId,
                profile.JobTitle,
                profile.SeniorityLevel,
                profile.YearsOfExperience))
            .ToList();
    }
}
