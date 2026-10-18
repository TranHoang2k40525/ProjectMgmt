namespace IdentityExperience.Domain.Entities;

public class OtpCode
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public int AttemptCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
