namespace IdentityExperience.Domain.Models;

public enum OtpIssueStatus
{
    Issued,
    RateLimited,
    UserNotFound,
    EmailAlreadyVerified,
    AccountDisabled,
    EmailNotVerified
}

public class OtpIssueResult
{
    public OtpIssueStatus Status { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int? RetryAfterSeconds { get; set; }
}
