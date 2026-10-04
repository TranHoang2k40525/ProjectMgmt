using IdentityExperience.Domain.Entities;

namespace IdentityExperience.Domain.Models;

public class ProfileDetails
{
    public User User { get; set; } = new();
    public UserProfile Profile { get; set; } = new();
    public List<UserSkillDetails> Skills { get; set; } = [];
}

public class UserSkillDetails
{
    public UserSkill UserSkill { get; set; } = new();
    public SkillCatalog Skill { get; set; } = new();
}

public enum ProfileUpdateStatus
{
    Updated,
    UserNotFound,
    PhoneNumberConflict
}

public class ProfileUpdateValues
{
    public string DisplayName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Timezone { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? SeniorityLevel { get; set; }
    public decimal? YearsOfExperience { get; set; }
    public string? Bio { get; set; }
}

public class AvatarUpdateResult
{
    public bool Updated { get; set; }
    public string? PreviousAvatarUrl { get; set; }
}

public enum SkillWriteStatus
{
    Updated,
    Created,
    UserNotFound,
    SkillNotFound,
    DuplicateCode,
    DuplicateSkill
}
