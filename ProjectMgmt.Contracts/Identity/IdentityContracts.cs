namespace ProjectMgmt.IdentityAccess.Contracts;

public class UserDisplayInfo
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}

public class UserSkillInfo
{
    public Guid UserId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public int ProficiencyLevel { get; set; }
}

public interface IUserLookupService
{
    Task<IReadOnlyList<UserDisplayInfo>> GetDisplayInfoAsync(IEnumerable<Guid> userIds);
    Task<UserDisplayInfo?> GetDisplayInfoAsync(Guid userId);
}

public interface IUserSkillService
{
    Task<IReadOnlyList<UserSkillInfo>> GetUserSkillsAsync(IEnumerable<Guid> userIds);
}
