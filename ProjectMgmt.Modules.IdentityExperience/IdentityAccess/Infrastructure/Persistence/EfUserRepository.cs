using Microsoft.EntityFrameworkCore;
using ProjectMgmt.Modules.IdentityExperience.IdentityAccess.Domain.Entities;
using ProjectMgmt.Modules.IdentityExperience.IdentityAccess.Domain.Repositories;
using ProjectMgmt.Modules.IdentityExperience.Infrastructure.Persistence;

namespace ProjectMgmt.Modules.IdentityExperience.IdentityAccess.Infrastructure.Persistence;

internal sealed class EfUserRepository(IdentityExperienceDbContext dbContext) : IUserRepository
{
    public Task<bool> ExistsActiveAsync(Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.Users.AsNoTracking().AnyAsync(x => x.Id == userId && x.IsActive, cancellationToken);

    public Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);

    public Task<User?> GetByNormalizedEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default) =>
        dbContext.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

    public Task<UserProfile?> GetActiveProfileAsync(Guid userId, CancellationToken cancellationToken = default) =>
        (from user in dbContext.Users.AsNoTracking()
         join profile in dbContext.UserProfiles.AsNoTracking() on user.Id equals profile.UserId
         where user.Id == userId && user.IsActive
         select profile)
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<UserProfile>> GetActiveProfilesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default) =>
        await (from user in dbContext.Users.AsNoTracking()
               join profile in dbContext.UserProfiles.AsNoTracking() on user.Id equals profile.UserId
               where userIds.Contains(user.Id) && user.IsActive
               select profile)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<UserSkillRecord>> GetSkillsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default) =>
        await (from userSkill in dbContext.UserSkills.AsNoTracking()
               join skill in dbContext.Skills.AsNoTracking() on userSkill.SkillId equals skill.Id
               where userIds.Contains(userSkill.UserId) && skill.IsActive
               select new UserSkillRecord(userSkill, skill))
            .ToListAsync(cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default) =>
        await dbContext.Users.AddAsync(user, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.SaveChangesAsync(cancellationToken);
}
