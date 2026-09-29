namespace IdentityExperience.Application.Dto;

public class ProfileDto : Result
{
    public Guid? UserId { get; set; }
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Bio { get; set; }
    public string? Timezone { get; set; }
    public string? JobTitle { get; set; }
    public string? SeniorityLevel { get; set; }
    public decimal? YearsOfExperience { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<SkillDto>? Skills { get; set; }
}

public class AvatarResult : Result
{
    public string? AvatarUrl { get; set; }
}
