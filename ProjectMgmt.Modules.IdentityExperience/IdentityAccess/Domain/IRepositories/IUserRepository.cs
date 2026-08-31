using ProjectMgmt.Modules.IdentityExperience.IdentityAccess.Domain.Entities;

namespace ProjectMgmt.Modules.IdentityExperience.IdentityAccess.Domain.IRepositories;

public interface IUserRepository
{
    Task<bool> ExistsActiveAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    Task<UserProfile?> GetActiveProfileAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserProfile>> GetActiveProfilesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserSkillRecord>> GetSkillsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public class UserSkillRecord
{
    public UserSkillRecord(UserSkill userSkill, SkillCatalog skill)
    {
        UserSkill = userSkill;
        Skill = skill;
    }

    public UserSkill UserSkill { get; }
    public SkillCatalog Skill { get; }
}
