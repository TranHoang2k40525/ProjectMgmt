namespace ProjectMgmt.IdentityAccess.Contracts;

public class UserDisplayInfo
{
    public UserDisplayInfo(Guid userId, string displayName, string? avatarUrl)
    {
        UserId = userId;
        DisplayName = displayName;
        AvatarUrl = avatarUrl;
    }

    public Guid UserId { get; set; }
    public string DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
}

public class UserSkillInfo
{
    public UserSkillInfo(
        Guid userId,
        string skillCode,
        string skillName,
        int proficiencyLevel,
        decimal? yearsOfExperience,
        bool isSelfDeclared,
        bool isVerified)
    {
        UserId = userId;
        SkillCode = skillCode;
        SkillName = skillName;
        ProficiencyLevel = proficiencyLevel;
        YearsOfExperience = yearsOfExperience;
        IsSelfDeclared = isSelfDeclared;
        IsVerified = isVerified;
    }

    public Guid UserId { get; set; }
    public string SkillCode { get; set; }
    public string SkillName { get; set; }
    public int ProficiencyLevel { get; set; }
    public decimal? YearsOfExperience { get; set; }
    public bool IsSelfDeclared { get; set; }
    public bool IsVerified { get; set; }
}

public class UserProfileFeatures
{
    public UserProfileFeatures(
        Guid userId,
        string? jobTitle,
        string? seniorityLevel,
        decimal? yearsOfExperience)
    {
        UserId = userId;
        JobTitle = jobTitle;
        SeniorityLevel = seniorityLevel;
        YearsOfExperience = yearsOfExperience;
    }

    public Guid UserId { get; set; }
    public string? JobTitle { get; set; }
    public string? SeniorityLevel { get; set; }
    public decimal? YearsOfExperience { get; set; }
}

public interface IUserLookupService
{
    Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<UserDisplayInfo?> GetDisplayInfoAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserDisplayInfo>> GetDisplayInfoAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);
}

public interface IUserSkillService
{
    Task<IReadOnlyList<UserSkillInfo>> GetUserSkillsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserProfileFeatures>> GetProfileFeaturesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);
}
