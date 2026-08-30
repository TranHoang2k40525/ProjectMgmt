namespace ProjectMgmt.IdentityAccess.Contracts;

public sealed record UserDisplayInfo(Guid UserId, string DisplayName, string? AvatarUrl);

public sealed record UserSkillInfo(
    Guid UserId,
    string SkillCode,
    string SkillName,
    int ProficiencyLevel,
    decimal? YearsOfExperience,
    bool IsSelfDeclared,
    bool IsVerified);

public sealed record UserProfileFeatures(
    Guid UserId,
    string? JobTitle,
    string? SeniorityLevel,
    decimal? YearsOfExperience);

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
