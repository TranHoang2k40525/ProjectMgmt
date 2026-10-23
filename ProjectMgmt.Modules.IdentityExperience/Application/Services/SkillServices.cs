using System.Text.RegularExpressions;
using IdentityExperience.Application.Dto;
using IdentityExperience.Application.IServices;
using IdentityExperience.Domain.Entities;
using IdentityExperience.Domain.IRepositories;
using IdentityExperience.Domain.Models;

namespace IdentityExperience.Application.Services;

public class SkillServices : ISkillServices
{
    private static readonly Regex SkillCodePattern = new(
        "^[a-z0-9][a-z0-9._+-]{1,59}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ISkillRepository _skillRepository;
    private readonly TimeProvider _timeProvider;

    public SkillServices(ISkillRepository skillRepository, TimeProvider timeProvider)
    {
        _skillRepository = skillRepository;
        _timeProvider = timeProvider;
    }

    public async Task<SkillDto> GetCatalogAsync(string? category, string? searchQuery)
    {
        if (category?.Trim().Length > 50 || searchQuery?.Trim().Length > 120)
        {
            return Failure("IDENTITY_SKILL_QUERY_INVALID", "Bộ lọc kỹ năng không hợp lệ.");
        }

        var catalog = await _skillRepository.GetCatalogAsync(category, searchQuery);
        return new SkillDto
        {
            Success = true,
            Items = catalog.Select(MapCatalog).ToList()
        };
    }

    public async Task<SkillDto> CreateCatalogSkillAsync(SkillDto request)
    {
        var code = request.Code?.Trim().ToLowerInvariant() ?? string.Empty;
        var name = request.Name?.Trim() ?? string.Empty;
        var category = request.Category?.Trim();

        if (!SkillCodePattern.IsMatch(code))
        {
            return Failure(
                "IDENTITY_SKILL_CODE_INVALID",
                "Mã kỹ năng phải dài 2-60 ký tự, bắt đầu bằng chữ/số và chỉ chứa chữ thường, số, dấu chấm, gạch hoặc dấu cộng.");
        }

        if (name.Length is < 2 or > 120)
        {
            return Failure("IDENTITY_SKILL_NAME_INVALID", "Tên kỹ năng phải có từ 2 đến 120 ký tự.");
        }

        if (category?.Length > 50)
        {
            return Failure("IDENTITY_SKILL_CATEGORY_INVALID", "Nhóm kỹ năng không được vượt quá 50 ký tự.");
        }

        var createResult = await _skillRepository.CreateCatalogSkillAsync(new SkillCatalog
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Category = string.IsNullOrWhiteSpace(category) ? null : category,
            IsActive = true
        });

        if (createResult.Status == SkillWriteStatus.DuplicateCode)
        {
            return Failure("IDENTITY_SKILL_CODE_EXISTS", "Mã kỹ năng đã tồn tại.");
        }

        var response = MapCatalog(createResult.Skill!);
        response.Success = true;
        response.Message = "Đã thêm kỹ năng vào danh mục.";
        return response;
    }

    public async Task<SkillDto> GetUserSkillsAsync(Guid userId)
    {
        if (userId == Guid.Empty || !await _skillRepository.UserExistsAsync(userId))
        {
            return Failure("IDENTITY_USER_NOT_FOUND", "Không tìm thấy người dùng.");
        }

        var skills = await _skillRepository.GetUserSkillsAsync(userId);
        return new SkillDto
        {
            Success = true,
            UserId = userId,
            Skills = skills.Select(MapUserSkill).ToList()
        };
    }

    public async Task<SkillDto> UpdateMySkillsAsync(Guid userId, List<SkillDto>? skills)
    {
        if (skills is null)
        {
            return Failure("IDENTITY_SKILLS_REQUIRED", "Phải truyền danh sách kỹ năng.");
        }

        if (skills.Count > 50)
        {
            return Failure("IDENTITY_SKILLS_LIMIT_EXCEEDED", "Mỗi người dùng có tối đa 50 kỹ năng.");
        }

        var duplicateSkill = skills
            .Where(skill => skill.SkillId.HasValue)
            .GroupBy(skill => skill.SkillId)
            .Any(group => group.Count() > 1);
        if (duplicateSkill)
        {
            return Failure("IDENTITY_SKILL_DUPLICATED", "Danh sách chứa kỹ năng bị lặp.");
        }

        var entities = new List<UserSkill>();
        foreach (var skill in skills)
        {
            var level = skill.ProficiencyLevel ?? skill.Level;
            if (!skill.SkillId.HasValue || skill.SkillId == Guid.Empty)
            {
                return Failure("IDENTITY_SKILL_ID_INVALID", "Mã kỹ năng không hợp lệ.");
            }

            if (level is < 1 or > 5)
            {
                return Failure("IDENTITY_SKILL_LEVEL_INVALID", "Mức kỹ năng phải từ 1 đến 5.");
            }

            if (skill.YearsOfExperience is < 0 or > 80)
            {
                return Failure("IDENTITY_SKILL_EXPERIENCE_INVALID", "Số năm kinh nghiệm phải từ 0 đến 80.");
            }

            entities.Add(new UserSkill
            {
                UserId = userId,
                SkillId = skill.SkillId.Value,
                ProficiencyLevel = level!.Value,
                YearsOfExperience = skill.YearsOfExperience,
                IsSelfDeclared = true
            });
        }

        var result = await _skillRepository.ReplaceUserSkillsAsync(
            userId,
            entities,
            _timeProvider.GetUtcNow().UtcDateTime);
        return result.Status switch
        {
            SkillWriteStatus.Updated => new SkillDto
            {
                Success = true,
                Message = "Cập nhật kỹ năng cá nhân thành công.",
                UserId = userId,
                UpdatedCount = result.UpdatedCount
            },
            SkillWriteStatus.UserNotFound =>
                Failure("IDENTITY_USER_NOT_FOUND", "Không tìm thấy người dùng."),
            SkillWriteStatus.DuplicateSkill =>
                Failure("IDENTITY_SKILL_DUPLICATED", "Danh sách chứa kỹ năng bị lặp."),
            _ => Failure("IDENTITY_SKILL_NOT_FOUND", "Có kỹ năng không tồn tại hoặc đã ngừng sử dụng.")
        };
    }

    public async Task<SkillDto> VerifyUserSkillAsync(
        Guid userId,
        Guid skillId,
        bool? verified,
        sbyte? level)
    {
        if (!verified.HasValue)
        {
            return Failure("IDENTITY_VERIFIED_REQUIRED", "Phải truyền trạng thái xác nhận.");
        }

        if (level is < 1 or > 5)
        {
            return Failure("IDENTITY_SKILL_LEVEL_INVALID", "Mức kỹ năng phải từ 1 đến 5.");
        }

        var status = await _skillRepository.VerifyUserSkillAsync(
            userId,
            skillId,
            verified.Value,
            level,
            _timeProvider.GetUtcNow().UtcDateTime);
        if (status != SkillWriteStatus.Updated)
        {
            return Failure("IDENTITY_USER_SKILL_NOT_FOUND", "Không tìm thấy kỹ năng của người dùng.");
        }

        return new SkillDto
        {
            Success = true,
            Message = verified.Value ? "Đã xác nhận kỹ năng." : "Đã hủy xác nhận kỹ năng.",
            UserId = userId,
            SkillId = skillId,
            ProficiencyLevel = level,
            IsSelfDeclared = !verified.Value,
            Verified = verified.Value
        };
    }

    private static SkillDto MapCatalog(SkillCatalog skill)
    {
        return new SkillDto
        {
            SkillId = skill.Id,
            Code = skill.Code,
            Name = skill.Name,
            Category = skill.Category,
            IsActive = skill.IsActive
        };
    }

    private static SkillDto MapUserSkill(UserSkillDetails details)
    {
        return new SkillDto
        {
            SkillId = details.Skill.Id,
            Code = details.Skill.Code,
            Name = details.Skill.Name,
            Category = details.Skill.Category,
            ProficiencyLevel = details.UserSkill.ProficiencyLevel,
            YearsOfExperience = details.UserSkill.YearsOfExperience,
            IsSelfDeclared = details.UserSkill.IsSelfDeclared,
            Verified = !details.UserSkill.IsSelfDeclared
        };
    }

    private static SkillDto Failure(string errorCode, string message)
    {
        return new SkillDto { Success = false, ErrorCode = errorCode, Message = message };
    }
}
