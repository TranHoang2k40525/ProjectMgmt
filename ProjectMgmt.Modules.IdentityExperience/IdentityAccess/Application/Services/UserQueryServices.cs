using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.Modules.IdentityExperience.IdentityAccess.Domain.Repositories;

namespace ProjectMgmt.Modules.IdentityExperience.IdentityAccess.Application.Services;

internal sealed class UserLookupService(IUserRepository repository) : IUserLookupService
{
    public Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        repository.ExistsActiveAsync(userId, cancellationToken);

    public async Task<UserDisplayInfo?> GetDisplayInfoAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var profile = await repository.GetActiveProfileAsync(userId, cancellationToken);
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

        return (await repository.GetActiveProfilesAsync(userIds, cancellationToken))
            .Select(profile => new UserDisplayInfo(profile.UserId, profile.DisplayName, profile.AvatarUrl))
            .ToList();
    }
}

internal sealed class UserSkillService(IUserRepository repository) : IUserSkillService
{
    public async Task<IReadOnlyList<UserSkillInfo>> GetUserSkillsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        return (await repository.GetSkillsAsync(userIds, cancellationToken))
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

        return (await repository.GetActiveProfilesAsync(userIds, cancellationToken))
            .Select(profile => new UserProfileFeatures(
                profile.UserId,
                profile.JobTitle,
                profile.SeniorityLevel,
                profile.YearsOfExperience))
            .ToList();
    }
}
