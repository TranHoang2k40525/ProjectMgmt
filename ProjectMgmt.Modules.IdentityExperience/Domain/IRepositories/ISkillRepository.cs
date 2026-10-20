using IdentityExperience.Domain.Entities;
using IdentityExperience.Domain.Models;

namespace IdentityExperience.Domain.IRepositories;

public interface ISkillRepository
{
    Task<List<SkillCatalog>> GetCatalogAsync(string? category, string? searchQuery);
    Task<(SkillWriteStatus Status, SkillCatalog? Skill)> CreateCatalogSkillAsync(SkillCatalog skill);
    Task<bool> UserExistsAsync(Guid userId);
    Task<List<UserSkillDetails>> GetUserSkillsAsync(Guid userId);
    Task<(SkillWriteStatus Status, int UpdatedCount)> ReplaceUserSkillsAsync(
        Guid userId,
        List<UserSkill> skills,
        DateTime nowUtc);
    Task<SkillWriteStatus> VerifyUserSkillAsync(
        Guid userId,
        Guid skillId,
        bool verified,
        sbyte? level,
        DateTime nowUtc);
}
