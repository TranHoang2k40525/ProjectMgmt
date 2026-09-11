namespace IdentityExperience.Domain.Entities;

public class UserProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? PhoneNumber { get; set; }
    public string Timezone { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? SeniorityLevel { get; set; }
    public decimal? YearsOfExperience { get; set; }
    public string? Bio { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
