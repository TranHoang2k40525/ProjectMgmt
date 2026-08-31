using Microsoft.EntityFrameworkCore;
using ProjectMgmt.Modules.IdentityExperience.IdentityAccess.Domain.Entities;
using ProjectMgmt.Modules.IdentityExperience.IdentityAccess.Domain.IRepositories;
using ProjectMgmt.Modules.IdentityExperience.Infrastructure.Persistence;

namespace ProjectMgmt.Modules.IdentityExperience.IdentityAccess.Infrastructure.Repositories;

internal class UserRepository : IUserRepository
{
    private readonly IdentityExperienceAppDbContext _dbContext;

    public UserRepository(IdentityExperienceAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    public Task<bool> ExistsActiveAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _dbContext.Users.AsNoTracking().AnyAsync(x => x.Id == userId && x.IsActive, cancellationToken);

    public Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _dbContext.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);

    public Task<User?> GetByNormalizedEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default) =>
        _dbContext.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

    public Task<UserProfile?> GetActiveProfileAsync(Guid userId, CancellationToken cancellationToken = default) =>
        (from user in _dbContext.Users.AsNoTracking()
         join profile in _dbContext.UserProfiles.AsNoTracking() on user.Id equals profile.UserId
         where user.Id == userId && user.IsActive
         select profile)
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<UserProfile>> GetActiveProfilesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default) =>
        await (from user in _dbContext.Users.AsNoTracking()
               join profile in _dbContext.UserProfiles.AsNoTracking() on user.Id equals profile.UserId
               where userIds.Contains(user.Id) && user.IsActive
               select profile)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<UserSkillRecord>> GetSkillsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default) =>
        await (from userSkill in _dbContext.UserSkills.AsNoTracking()
               join skill in _dbContext.Skills.AsNoTracking() on userSkill.SkillId equals skill.Id
               where userIds.Contains(userSkill.UserId) && skill.IsActive
               select new UserSkillRecord(userSkill, skill))
            .ToListAsync(cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default) =>
        await _dbContext.Users.AddAsync(user, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.SaveChangesAsync(cancellationToken);
}
