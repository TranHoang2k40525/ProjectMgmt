using IdentityExperience.Application.Dto;

namespace IdentityExperience.Application.IServices;

public interface ISkillServices
{
    Task<SkillDto> GetCatalogAsync(string? category, string? searchQuery);
    Task<SkillDto> CreateCatalogSkillAsync(SkillDto request);
    Task<SkillDto> GetUserSkillsAsync(Guid userId);
    Task<SkillDto> UpdateMySkillsAsync(Guid userId, List<SkillDto>? skills);
    Task<SkillDto> VerifyUserSkillAsync(
        Guid userId,
        Guid skillId,
        bool? verified,
        sbyte? level);
}
