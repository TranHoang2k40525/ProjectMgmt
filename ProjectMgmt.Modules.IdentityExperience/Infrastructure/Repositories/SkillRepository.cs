using IdentityExperience.Domain.Entities;
using IdentityExperience.Domain.IRepositories;
using IdentityExperience.Domain.Models;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace IdentityExperience.Infrastructure.Repository;

public class SkillRepository : ISkillRepository
{
    private readonly IdentityExperienceDbContext _context;

    public SkillRepository(IdentityExperienceDbContext context)
    {
        _context = context;
    }

    public Task<List<SkillCatalog>> GetCatalogAsync(string? category, string? searchQuery)
    {
        var query = _context.SkillCatalogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(category))
        {
            var normalizedCategory = category.Trim();
            query = query.Where(skill => skill.Category == normalizedCategory);
        }

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var term = searchQuery.Trim();
            query = query.Where(skill => skill.Name.Contains(term) || skill.Code.Contains(term));
        }

        return query
            .OrderByDescending(skill => skill.IsActive)
            .ThenBy(skill => skill.Category)
            .ThenBy(skill => skill.Name)
            .ToListAsync();
    }

    public async Task<(SkillWriteStatus Status, SkillCatalog? Skill)> CreateCatalogSkillAsync(
        SkillCatalog skill)
    {
        try
        {
            await _context.SkillCatalogs.AddAsync(skill);
            await _context.SaveChangesAsync();
            return (SkillWriteStatus.Created, skill);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is MySqlException { Number: 1062 })
        {
            _context.ChangeTracker.Clear();
            return (SkillWriteStatus.DuplicateCode, null);
        }
    }

    public Task<bool> UserExistsAsync(Guid userId)
    {
        return _context.Users.AsNoTracking().AnyAsync(user => user.Id == userId);
    }

    public Task<List<UserSkillDetails>> GetUserSkillsAsync(Guid userId)
    {
        return (from userSkill in _context.UserSkills.AsNoTracking()
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
    }

    public async Task<(SkillWriteStatus Status, int UpdatedCount)> ReplaceUserSkillsAsync(
        Guid userId,
        List<UserSkill> skills,
        DateTime nowUtc)
    {
        var executionStrategy = _context.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var lockedUsers = await _context.Users
                .FromSqlInterpolated($"SELECT * FROM `User` WHERE `Id` = {userId} FOR UPDATE")
                .ToListAsync();
            if (lockedUsers.Count == 0)
            {
                await transaction.RollbackAsync();
                return (SkillWriteStatus.UserNotFound, 0);
            }

            var requestedSkillIds = skills.Select(skill => skill.SkillId).Distinct().ToList();
            if (requestedSkillIds.Count != skills.Count)
            {
                await transaction.RollbackAsync();
                return (SkillWriteStatus.DuplicateSkill, 0);
            }

            var activeSkillCount = await _context.SkillCatalogs
                .CountAsync(skill => requestedSkillIds.Contains(skill.Id) && skill.IsActive);
            if (activeSkillCount != requestedSkillIds.Count)
            {
                await transaction.RollbackAsync();
                return (SkillWriteStatus.SkillNotFound, 0);
            }

            var existingSkills = await _context.UserSkills
                .Where(skill => skill.UserId == userId)
                .ToListAsync();

            var retainedSkillIds = requestedSkillIds.ToHashSet();
            _context.UserSkills.RemoveRange(
                existingSkills.Where(skill => !retainedSkillIds.Contains(skill.SkillId)));

            foreach (var requestedSkill in skills)
            {
                var existingSkill = existingSkills.FirstOrDefault(
                    skill => skill.SkillId == requestedSkill.SkillId);
                if (existingSkill is null)
                {
                    requestedSkill.Id = Guid.NewGuid();
                    requestedSkill.UserId = userId;
                    requestedSkill.IsSelfDeclared = true;
                    requestedSkill.UpdatedAt = nowUtc;
                    await _context.UserSkills.AddAsync(requestedSkill);
                    continue;
                }

                existingSkill.ProficiencyLevel = requestedSkill.ProficiencyLevel;
                existingSkill.YearsOfExperience = requestedSkill.YearsOfExperience;
                existingSkill.IsSelfDeclared = true;
                existingSkill.UpdatedAt = nowUtc;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return (SkillWriteStatus.Updated, skills.Count);
        });
    }

    public async Task<SkillWriteStatus> VerifyUserSkillAsync(
        Guid userId,
        Guid skillId,
        bool verified,
        sbyte? level,
        DateTime nowUtc)
    {
        var executionStrategy = _context.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var lockedSkills = await _context.UserSkills
                .FromSqlInterpolated($"""
                    SELECT * FROM `UserSkill`
                    WHERE `UserId` = {userId} AND `SkillId` = {skillId}
                    FOR UPDATE
                    """)
                .ToListAsync();
            var userSkill = lockedSkills.SingleOrDefault();
            if (userSkill is null)
            {
                await transaction.RollbackAsync();
                return SkillWriteStatus.SkillNotFound;
            }

            if (level.HasValue)
            {
                userSkill.ProficiencyLevel = level.Value;
            }

            userSkill.IsSelfDeclared = !verified;
            userSkill.UpdatedAt = nowUtc;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return SkillWriteStatus.Updated;
        });
    }
}
