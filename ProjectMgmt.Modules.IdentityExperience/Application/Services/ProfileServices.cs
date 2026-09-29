using IdentityExperience.Application.Dto;
using IdentityExperience.Application.IServices;
using IdentityExperience.Domain.IRepositories;
using IdentityExperience.Domain.Models;
using PhoneNumbers;

namespace IdentityExperience.Application.Services;

public class ProfileServices : IProfileServices
{
    private const long MaximumAvatarBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> SeniorityLevels =
        ["Intern", "Junior", "Middle", "Senior", "Lead", "Principal"];

    private readonly IProfileRepository _profileRepository;
    private readonly IAvatarStorage _avatarStorage;
    private readonly TimeProvider _timeProvider;

    public ProfileServices(
        IProfileRepository profileRepository,
        IAvatarStorage avatarStorage,
        TimeProvider timeProvider)
    {
        _profileRepository = profileRepository;
        _avatarStorage = avatarStorage;
        _timeProvider = timeProvider;
    }

    public async Task<ProfileDto> GetMyProfileAsync(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return Failure("IDENTITY_UNAUTHENTICATED", "Phiên đăng nhập không hợp lệ.");
        }

        var details = await _profileRepository.GetProfileDetailsAsync(userId);
        return details is null
            ? Failure("IDENTITY_PROFILE_NOT_FOUND", "Không tìm thấy hồ sơ người dùng.")
            : Map(details);
    }

    public async Task<ProfileDto> UpdateMyProfileAsync(Guid userId, ProfileDto request)
    {
        var current = await _profileRepository.GetProfileDetailsAsync(userId);
        if (current is null)
        {
            return Failure("IDENTITY_PROFILE_NOT_FOUND", "Không tìm thấy hồ sơ người dùng.");
        }

        var displayName = request.FullName is null
            ? current.Profile.DisplayName
            : request.FullName.Trim();
        if (displayName.Length is < 2 or > 150)
        {
            return Failure("IDENTITY_FULL_NAME_INVALID", "Họ tên phải có từ 2 đến 150 ký tự.");
        }

        var phoneNumber = current.Profile.PhoneNumber;
        if (request.PhoneNumber is not null)
        {
            if (string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                phoneNumber = null;
            }
            else if (!TryNormalizePhone(request.PhoneNumber, out phoneNumber))
            {
                return Failure("IDENTITY_PHONE_INVALID", "Số điện thoại không hợp lệ.");
            }
        }

        var timezone = request.Timezone is null
            ? current.Profile.Timezone
            : request.Timezone.Trim();
        if (!IsValidTimezone(timezone))
        {
            return Failure("IDENTITY_TIMEZONE_INVALID", "Múi giờ không hợp lệ.");
        }

        var jobTitle = request.JobTitle is null
            ? current.Profile.JobTitle
            : OptionalText(request.JobTitle);
        var bio = request.Bio is null
            ? current.Profile.Bio
            : OptionalText(request.Bio);
        var seniority = request.SeniorityLevel is null
            ? current.Profile.SeniorityLevel
            : OptionalText(request.SeniorityLevel);
        var years = request.YearsOfExperience ?? current.Profile.YearsOfExperience;

        if (jobTitle?.Length > 150)
        {
            return Failure("IDENTITY_JOB_TITLE_TOO_LONG", "Chức danh không được vượt quá 150 ký tự.");
        }

        if (bio?.Length > 5_000)
        {
            return Failure("IDENTITY_BIO_TOO_LONG", "Giới thiệu không được vượt quá 5.000 ký tự.");
        }

        if (seniority is not null && !SeniorityLevels.Contains(seniority))
        {
            return Failure(
                "IDENTITY_SENIORITY_INVALID",
                "Cấp độ phải là Intern, Junior, Middle, Senior, Lead hoặc Principal.");
        }

        if (years is < 0 or > 80)
        {
            return Failure("IDENTITY_EXPERIENCE_INVALID", "Số năm kinh nghiệm phải từ 0 đến 80.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var status = await _profileRepository.UpdateProfileAsync(
            userId,
            new ProfileUpdateValues
            {
                DisplayName = displayName,
                PhoneNumber = phoneNumber,
                Timezone = timezone,
                JobTitle = jobTitle,
                SeniorityLevel = seniority,
                YearsOfExperience = years,
                Bio = bio
            },
            now);

        if (status == ProfileUpdateStatus.PhoneNumberConflict)
        {
            return Failure("IDENTITY_PHONE_ALREADY_EXISTS", "Số điện thoại đã được sử dụng.");
        }

        if (status != ProfileUpdateStatus.Updated)
        {
            return Failure("IDENTITY_PROFILE_NOT_FOUND", "Không tìm thấy hồ sơ người dùng.");
        }

        var updated = await _profileRepository.GetProfileDetailsAsync(userId);
        if (updated is null)
        {
            return Failure("IDENTITY_PROFILE_NOT_FOUND", "Không tìm thấy hồ sơ người dùng.");
        }

        var response = Map(updated);
        response.Message = "Cập nhật hồ sơ thành công.";
        response.UpdatedAt = now;
        return response;
    }

    public async Task<AvatarResult> UpdateAvatarAsync(
        Guid userId,
        Stream content,
        long length,
        string? contentType)
    {
        _ = contentType;
        if (userId == Guid.Empty)
        {
            return AvatarFailure("IDENTITY_UNAUTHENTICATED", "Phiên đăng nhập không hợp lệ.");
        }

        if (length is <= 0 or > MaximumAvatarBytes)
        {
            return AvatarFailure(
                "IDENTITY_AVATAR_SIZE_INVALID",
                "Ảnh đại diện phải lớn hơn 0 byte và không vượt quá 5 MB.");
        }

        await using var buffer = new MemoryStream((int)length);
        await content.CopyToAsync(buffer);
        if (buffer.Length != length || buffer.Length > MaximumAvatarBytes)
        {
            return AvatarFailure("IDENTITY_AVATAR_SIZE_INVALID", "Kích thước ảnh đại diện không hợp lệ.");
        }

        var extension = DetectImageExtension(buffer.GetBuffer(), (int)buffer.Length);
        if (extension is null)
        {
            return AvatarFailure(
                "IDENTITY_AVATAR_TYPE_INVALID",
                "Chỉ chấp nhận ảnh JPEG, PNG hoặc WebP hợp lệ.");
        }

        buffer.Position = 0;
        var avatarUrl = await _avatarStorage.SaveAsync(userId, buffer, extension);
        var update = await _profileRepository.UpdateAvatarUrlAsync(
            userId,
            avatarUrl,
            _timeProvider.GetUtcNow().UtcDateTime);
        if (!update.Updated)
        {
            await _avatarStorage.DeleteAsync(avatarUrl);
            return AvatarFailure("IDENTITY_PROFILE_NOT_FOUND", "Không tìm thấy hồ sơ người dùng.");
        }

        await _avatarStorage.DeleteAsync(update.PreviousAvatarUrl);
        return new AvatarResult
        {
            Success = true,
            Message = "Cập nhật ảnh đại diện thành công.",
            AvatarUrl = avatarUrl
        };
    }

    private static ProfileDto Map(ProfileDetails details)
    {
        return new ProfileDto
        {
            Success = true,
            UserId = details.User.Id,
            Email = details.User.Email,
            FullName = details.Profile.DisplayName,
            AvatarUrl = details.Profile.AvatarUrl,
            PhoneNumber = details.Profile.PhoneNumber,
            Bio = details.Profile.Bio,
            Timezone = details.Profile.Timezone,
            JobTitle = details.Profile.JobTitle,
            SeniorityLevel = details.Profile.SeniorityLevel,
            YearsOfExperience = details.Profile.YearsOfExperience,
            UpdatedAt = details.Profile.UpdatedAt,
            Skills = details.Skills.Select(skill => new SkillDto
            {
                SkillId = skill.Skill.Id,
                Code = skill.Skill.Code,
                Name = skill.Skill.Name,
                Category = skill.Skill.Category,
                ProficiencyLevel = skill.UserSkill.ProficiencyLevel,
                YearsOfExperience = skill.UserSkill.YearsOfExperience,
                IsSelfDeclared = skill.UserSkill.IsSelfDeclared,
                Verified = !skill.UserSkill.IsSelfDeclared
            }).ToList()
        };
    }

    private static ProfileDto Failure(string errorCode, string message)
    {
        return new ProfileDto { Success = false, ErrorCode = errorCode, Message = message };
    }

    private static AvatarResult AvatarFailure(string errorCode, string message)
    {
        return new AvatarResult { Success = false, ErrorCode = errorCode, Message = message };
    }

    private static bool TryNormalizePhone(string input, out string? phoneNumber)
    {
        phoneNumber = null;
        try
        {
            var utility = PhoneNumberUtil.GetInstance();
            var parsed = utility.Parse(input.Trim(), "VN");
            if (!utility.IsValidNumber(parsed))
            {
                return false;
            }

            phoneNumber = utility.Format(parsed, PhoneNumberFormat.E164);
            return true;
        }
        catch (NumberParseException)
        {
            return false;
        }
    }

    private static bool IsValidTimezone(string timezone)
    {
        if (timezone.Length is 0 or > 64)
        {
            return false;
        }

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timezone);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    private static string? OptionalText(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static string? DetectImageExtension(byte[] bytes, int length)
    {
        if (length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return ".jpg";
        }

        if (length >= 8
            && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
            && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
        {
            return ".png";
        }

        if (length >= 12
            && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
            && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
        {
            return ".webp";
        }

        return null;
    }
}
