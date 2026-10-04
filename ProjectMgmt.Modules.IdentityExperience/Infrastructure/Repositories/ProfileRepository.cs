using IdentityExperience.Domain.IRepositories;
using IdentityExperience.Domain.Models;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace IdentityExperience.Infrastructure.Repository;

public class ProfileRepository : IProfileRepository
{
    private readonly IdentityExperienceDbContext _context;

    public ProfileRepository(IdentityExperienceDbContext context)
    {
        _context = context;
    }

    public async Task<ProfileDetails?> GetProfileDetailsAsync(Guid userId)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == userId);
        if (user is null)
        {
            return null;
        }

        var profile = await _context.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.UserId == userId);
        if (profile is null)
        {
            return null;
        }

        var skills = await (from userSkill in _context.UserSkills.AsNoTracking()
                            join skill in _context.SkillCatalogs.AsNoTracking()
                                on userSkill.SkillId equals skill.Id
                            where userSkill.UserId == userId
                            orderby skill.Category, skill.Name
                            select new UserSkillDetails
                            {
                                UserSkill = userSkill,
                                Skill = skill
                            })
            .ToListAsync();

        return new ProfileDetails
        {
            User = user,
            Profile = profile,
            Skills = skills
        };
    }

    public async Task<ProfileUpdateStatus> UpdateProfileAsync(
        Guid userId,
        ProfileUpdateValues values,
        DateTime nowUtc)
    {
        var executionStrategy = _context.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var lockedProfiles = await _context.UserProfiles
                .FromSqlInterpolated($"SELECT * FROM `UserProfile` WHERE `UserId` = {userId} FOR UPDATE")
                .ToListAsync();
            var profile = lockedProfiles.SingleOrDefault();
            if (profile is null)
            {
                await transaction.RollbackAsync();
                return ProfileUpdateStatus.UserNotFound;
            }

            profile.DisplayName = values.DisplayName;
            profile.PhoneNumber = values.PhoneNumber;
            profile.Timezone = values.Timezone;
            profile.JobTitle = values.JobTitle;
            profile.SeniorityLevel = values.SeniorityLevel;
            profile.YearsOfExperience = values.YearsOfExperience;
            profile.Bio = values.Bio;
            profile.UpdatedAt = nowUtc;

            try
            {
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return ProfileUpdateStatus.Updated;
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is MySqlException { Number: 1062 })
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                return ProfileUpdateStatus.PhoneNumberConflict;
            }
        });
    }

    public async Task<AvatarUpdateResult> UpdateAvatarUrlAsync(
        Guid userId,
        string avatarUrl,
        DateTime nowUtc)
    {
        var executionStrategy = _context.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var lockedProfiles = await _context.UserProfiles
                .FromSqlInterpolated($"SELECT * FROM `UserProfile` WHERE `UserId` = {userId} FOR UPDATE")
                .ToListAsync();
            var profile = lockedProfiles.SingleOrDefault();
            if (profile is null)
            {
                await transaction.RollbackAsync();
                return new AvatarUpdateResult { Updated = false };
            }

            var previousAvatarUrl = profile.AvatarUrl;
            profile.AvatarUrl = avatarUrl;
            profile.UpdatedAt = nowUtc;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return new AvatarUpdateResult
            {
                Updated = true,
                PreviousAvatarUrl = previousAvatarUrl
            };
        });
    }
}
