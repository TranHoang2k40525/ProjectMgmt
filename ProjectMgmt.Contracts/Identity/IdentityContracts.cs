namespace ProjectMgmt.IdentityAccess.Contracts;

public record UserDisplayInfo(Guid UserId, string DisplayName, string? AvatarUrl);

public record UserSkillInfo(Guid UserId, string SkillName, int ProficiencyLevel);

public interface IUserLookupService
{
    Task<IReadOnlyList<UserDisplayInfo>> GetDisplayInfoAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default);
    Task<UserDisplayInfo?> GetDisplayInfoAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IUserSkillService
{
    Task<IReadOnlyList<UserSkillInfo>> GetUserSkillsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default);
}
